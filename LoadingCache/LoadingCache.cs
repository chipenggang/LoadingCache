using LoadingCache;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace LoadingCache
{
    public class LoadingCache<V> : ILoadingCache<V> //where V : CacheDataTime
    {
        #region * filed
        private readonly CacheBuilder<V> cacheBuilder;
        private Func<string, V> load;
        private bool lockState = false;
        private static int IntervalMillSeconed = 60 * 1000;
        private LRUCache<V> lruCache;
        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cacheBuilder"></param>
        /// <param name="load"></param>
        public LoadingCache(CacheBuilder<V> cacheBuilder, Func<string, V> load)
        {
            this.cacheBuilder = cacheBuilder;
            this.load = load;
            lruCache = new LRUCache<V>(this.cacheBuilder.InitCapacity);
            AutoReSize();
        }

        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public V GetIfPresent(string key)
        {
            var cache = GetCacheData(key);
            if (cache.Item1 == null || !cache.Item2)
            {
                return default(V);
            }

            return cache.Item1;
        }

        /// <summary>
        /// 获取数据，缓存没有则从委托查询
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public V Get(string key)
        {
            var cache = GetCacheData(key);
            if (cache.Item1 != null && cache.Item2)
            {
                return cache.Item1;
            }

            if (cache.Item1 == null)
            {
                //压根没缓存,全部阻塞
                return GetCacheFromLoad(key);
            }

            if (cache.Item2)
            {
                return cache.Item1;
            }

            //异步刷新未配置，全部阻塞，重新获取
            if (this.cacheBuilder.RefreshAfterWriteMilliseconds <= 0)
            {
                return GetCacheFromLoad(key);
            }
            
            //异步刷新缓存
            if (this.cacheBuilder.AutoRefresh)
            {
                //自动刷新的，直接后台线程处理，直接返回旧值即可
                Task.Run(() =>
                {
                    if (!lockState)
                    {
                        //Console.WriteLine("后台执行缓存刷新，当前缓存值为：" + cache.Item1.ToString()+", 线程Id："+Task.CurrentId);
                        GetCacheFromLoad(key);
                    }
                });

                return cache.Item1;
            }

            //启用一个线程刷新，其他线程不阻塞
            if (!lockState)
            {
                return GetCacheFromLoad(key);
            }

            return cache.Item1;
        }

        /// <summary>
        /// 添加缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, V value)
        {
            //设置缓存时间
            var cacheItemPolicy = new CacheItemPolicy();

            var expireTime = Math.Max(cacheBuilder.ExpireAfterAccessMilliseconds, cacheBuilder.ExpireAfterWriteMilliseconds);
            //设置缓存过期时需要注意，如果有设置刷新时间，则缓存不能过期，因为只有单线程更新缓存影响其他线程获取数据
            if (this.cacheBuilder.RefreshAfterWriteMilliseconds <= 0 && expireTime > 0)
            {
                //设置缓存时间
                cacheItemPolicy.SlidingExpiration = TimeSpan.FromMilliseconds(expireTime);
            }
            else
            {
                ////设置了自动刷新缓存，或者未设置缓存时间，缓存永久不失效
                cacheItemPolicy.Priority = CacheItemPriority.NotRemovable;
            }

            lruCache.Set(key, value, cacheItemPolicy);
        }

        /// <summary>
        /// 获取所有有效缓存
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public Dictionary<string, V> GetAllPresent(List<string> keys)
        {
            var dic = new Dictionary<string, V>();

            foreach (var key in keys)
            {
                var value = GetIfPresent(key);
                if (value != null)
                {
                    dic.Add(key, value);
                }
            }

            return dic;
        }

        /// <summary>
        /// 使缓存失效
        /// </summary>
        /// <param name="key"></param>
        public void Invalidate(string key)
        {
            lruCache.Remove(key);
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private V GetCacheFromLoad(string key)
        {
            V value = default(V);

            //暂时简单处理锁，此处需要根据key值不同获取不同锁
            lock (this)
            {
                lockState = true;
                value = GetIfPresent(key);
                if (null == value)
                {
                    value = load(key);
                    if (value != null)
                    {
                        Set(key, value);
                    }
                }
            }
            lockState = false;

            return value;
        }

        /// <summary>
        /// 获取缓存数据，并指示缓存是否有效
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private Tuple<V, bool> GetCacheData(string key)
        {
            var value = lruCache.Get(key);
            if (value.Item1 == null)
            {
                return new Tuple<V, bool>(default(V), false);
            }

            var accessT = (DateTime.Now - value.Item2).TotalMilliseconds;
            var writeT = (DateTime.Now - value.Item3).TotalMilliseconds;

            if (cacheBuilder.RefreshAfterWriteMilliseconds > 0 && cacheBuilder.RefreshAfterWriteMilliseconds < writeT)
            {
                //缓存失效
                return new Tuple<V, bool>(value.Item1, false);
            }

            //缓存是否有效
            var cacheValidate = (cacheBuilder.ExpireAfterAccessMilliseconds <= 0 || cacheBuilder.ExpireAfterAccessMilliseconds > accessT)
                && (cacheBuilder.ExpireAfterWriteMilliseconds <= 0 || cacheBuilder.ExpireAfterWriteMilliseconds > writeT);

            //需要根据时间刷新缓存
            if (!cacheValidate)
            {
                //缓存失效，清除缓存
                lruCache.Remove(key);
                return new Tuple<V, bool>(default(V), false);
            }

            return new Tuple<V, bool>(value.Item1, true);
        }

        /// <summary>
        /// 定时调整数据大小
        /// </summary>
        private void AutoReSize()
        {
            Task.Run(()=>
            {
                Timer timer = new Timer();
                timer.AutoReset = true;
                timer.Enabled = true;
                timer.Interval = IntervalMillSeconed;
                timer.Elapsed += Timer_Elapsed;
                timer.Start();
            });
        }

        /// <summary>
        /// 定时刷新,检测内存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (this.cacheBuilder.AutoResize)
            {
                ///根据内存大小分配最大容量
                //var phyCacheMemoryLimit = cache.PhysicalMemoryLimit;
                //var memoryLimit = cache.CacheMemoryLimit;

                var calSize = 100000; //计算可分配使用量 todo 待完善





                this.lruCache.Capacity = calSize;
            }
        }
    }
}
