using HNService.Models;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Retry;
using System.Text.Json;

namespace HNService.Services
{
    public interface IHNDataService
    {
        public Task<HNData[]> GetBestStories(int count);
        public int Count { get; }
    }

    internal class HNDataService : IHNDataService
    {
        private readonly ILogger<HNDataService> _logger;
        private readonly IConfiguration _configuration;
        private readonly bool _allowedCaching = false;
        private readonly int _cachRefreshIntervalInMinutes = 30;
        private readonly Policy _cacheRefreshPolicy;
        private readonly Policy _cacheRefreshCanellablePolicy;
        private readonly RetryPolicy<HttpResponseMessage> _retryWebCallPolicy;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly MemoryCache _cache;
        private readonly int _numberOfCores;
        private readonly Uri _baseUri;
        private readonly HttpClient _httpClient;
        private readonly object _dataLock = new object();
        private readonly List<HNData> _models = new List<HNData>();
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        TimeSpan cacheDuration = TimeSpan.FromMinutes(30);


        public HNDataService(ILogger<HNDataService> logger, IConfiguration configuration, CancellationTokenSource cancellationTokenSource) 
        {
            _logger = logger;
            _configuration = configuration;
            
            var allowedCachingStr = _configuration["allowedCaching"];
            if (!string.IsNullOrEmpty(allowedCachingStr) && bool.TryParse(allowedCachingStr, out _allowedCaching))
            {
                _logger.LogInformation(string.Format("Caching is allowed : {0}", _allowedCaching.ToString()));
            }

            var cachRefreshIntervalInMinutesStr = _configuration["cachRefreshIntervalInMinutes"];
            if (!string.IsNullOrEmpty(cachRefreshIntervalInMinutesStr) && int.TryParse(cachRefreshIntervalInMinutesStr, out _cachRefreshIntervalInMinutes))
            {
                _logger.LogInformation(string.Format("Caching refresh interval in minutes : {0}", _cachRefreshIntervalInMinutes));
            }

            _cache = new MemoryCache(new MemoryCacheOptions());
            _baseUri = new Uri(@"https://hacker-news.firebaseio.com/v0/");
            _httpClient = new HttpClient() { BaseAddress = _baseUri };
            _numberOfCores = Environment.ProcessorCount <= 2 ? Environment.ProcessorCount : Environment.ProcessorCount - 2;
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
            };

            // Policy for web call
            _retryWebCallPolicy = Policy
            .Handle<HttpRequestException>((ex) => {
                _logger.LogCritical(ex.Message, ex);
                return true;
            })
            .OrResult<HttpResponseMessage>(result => !result.IsSuccessStatusCode) 
            .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            // Policy for canellable cache refresh
            _cacheRefreshCanellablePolicy = Policy
                .Handle<Exception>((ex) => { 
                    _logger.LogCritical(ex.Message);
                    return true;
                })
                .Retry(3, (ex, retryCount) =>
                {
                    _logger.LogInformation(string.Format("Cache refresh failed. Retry attempt: {0}", retryCount));
                } )
                ;

            _cancellationTokenSource = cancellationTokenSource;

            // Policy for cache refresh
            _cacheRefreshPolicy = Policy
                .Handle<Exception>((ex) => {
                    _logger.LogCritical(ex.Message);
                    return true;
                })
                .Retry(3, (ex, retryCount) =>
                {
                    _logger.LogInformation(string.Format("Cache refresh failed. Retry attempt: {0}", retryCount));
                });

            if (_allowedCaching)
            {
                RefreshCache(_cache, _cacheRefreshPolicy, cacheDuration, _cancellationTokenSource.Token);

                Timer timer = new Timer(_ =>
                {
                    _cacheRefreshPolicy.Execute(() => { return LoadData(_cancellationTokenSource.Token); });
                }, null, TimeSpan.Zero, TimeSpan.FromMinutes(_cachRefreshIntervalInMinutes));
            }
        }
        void RefreshCache(MemoryCache cache, Policy cacheRefreshPolicy, TimeSpan cacheDuration, CancellationToken cancellationToken)
        {
            List<HNData> refreshedData = cacheRefreshPolicy.Execute(() => LoadData(cancellationToken).Result);
            cache.Set("HNItemList", refreshedData, DateTime.Now.Add(cacheDuration));
            _logger.LogInformation(string.Format("Cache refreshed at: {0}", DateTime.Now));
        }

        private List<HNData> RetrieveFromCache()
        {
            List<HNData> cachedList = _cache.Get<List<HNData>>("HNItemList");
            if (cachedList == null)
            {
                cachedList = _cacheRefreshCanellablePolicy.Execute(() =>
                {
                    return LoadData(_cancellationTokenSource.Token).Result;
                });
                _cache.Set("HNItemList", cachedList, cacheDuration);
                _logger.LogInformation(string.Format("Data retrieved from the source and added to the cache."));
            }
            else
            {
                _logger.LogInformation(string.Format("Data retrieved from the cache."));
            }
            return cachedList;
        }

        private Task<List<HNData>> LoadData(CancellationToken cancellationToken)
        {
            var allStories = GetAllStories();

            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _numberOfCores,
                CancellationToken = cancellationToken
            };

            List<HNItem> allItems = new List<HNItem>();
            Parallel.ForEach(allStories, parallelOptions, story =>
            {
                var item = GetStory(story).Result;
                allItems.Add(item);
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation(string.Format("Refresh cancellation requested."));
                    return;
                }
            });

            lock (_dataLock)
            {
                _models.Clear();
                _models.AddRange(allItems.Select( x => new HNData(){
                    title = x.Title,
                    uri = x.Url,
                    postedBy = x.By,
                    time = x.Time,
                    score = x.Score, 
                    commentCount = x.Kids?.Count ?? 0,
                }).OrderByDescending(x => x.score));
            }
            return Task.FromResult(_models);
        }
        public int Count
        {
            get
            {
                lock (_dataLock)
                {
                    return _models.Count;
                }
            }
        }

        private List<int> GetAllStories()
        {
            List<int>? result = null;
            HttpResponseMessage response = _retryWebCallPolicy.Execute(() =>
            {
                return _httpClient.GetAsync("beststories.json").Result;
            });

            response.EnsureSuccessStatusCode();
            var stream = response.Content.ReadAsStream();
            if (stream != null && stream.CanRead)
                result = JsonSerializer.Deserialize<List<int>>(stream);
            return result ?? new List<int>();
        }
        private async Task<HNItem> GetStory(int id)
        {
            HttpResponseMessage response = _retryWebCallPolicy.Execute(() =>
            {
                return _httpClient.GetAsync(new Uri(_baseUri.ToString() + $"item/{id}.json")).Result;
            });
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<HNItem>(responseBody, _serializerOptions);
        }


        public Task<HNData[]> GetBestStories(int count)
        {
            lock (_dataLock)
            {
                if (_allowedCaching)
                {
                    return Task.FromResult(RetrieveFromCache().Take(count).ToArray());
                }
                else
                {
                    LoadData(_cancellationTokenSource.Token);
                    return Task.FromResult(_models.Take(count).ToArray());
                }
            }
        }
    }
}
