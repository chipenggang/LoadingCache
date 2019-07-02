using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoadingCache
{
    public class LRUCache<V>
    {
        private const int DEFAULT_CAPACITY = 255;
        private readonly MemoryCache cache;
        private long _capacity;//缓存容量 

        private ReaderWriterLockSlim _locker;
        private LinkedList<CacheData> _linkedList;
        private CacheEntryRemovedCallback removedCallback;

        public LRUCache(int capacity)
        {
            _locker = new ReaderWriterLockSlim();
            cache = new MemoryCache(Guid.NewGuid().ToString());
            _capacity = capacity > 0 ? capacity : DEFAULT_CAPACITY;
            _linkedList = new LinkedList<CacheData>();
            removedCallback += AfterCacheRemove;
        }

        public void Set(string key, V value, CacheItemPolicy cacheItemPolicy)
        {
            _locker.EnterWriteLock();
            try
            {
                if (cache.Contains(key))
                {
                    //异步更新LRU顺序表
                    Task.Run(() =>
                    {
                        var linkCache = _linkedList.FirstOrDefault(p => p.Key == key);
                        if (linkCache != null)
                        {
                            LRUReSort(linkCache, true);
                        }
                    });
                }
                else
                {
                    var cacheData = new CacheData() { Key = key, AccessTime = DateTime.Now, WriteTime = DateTime.Now };
                    _linkedList.AddFirst(cacheData);
                }
                cacheItemPolicy.RemovedCallback = removedCallback;
                cache.Set(key, value, cacheItemPolicy);

                if (_linkedList.Count > _capacity)
                {
                    cache.Remove(_linkedList.Last.Value.Key);
                }
            }
            finally { _locker.ExitWriteLock(); }
        }

        /// <summary>
        /// 缓存重排序
        /// </summary>
        /// <param name="linkCache"></param>
        /// <param name="write"></param>
        private void LRUReSort(CacheData linkCache, bool write = false)
        {
            lock (_linkedList)
            {
                //LRU重排
                _linkedList.Remove(linkCache);
                //修改访问时间
                linkCache.AccessTime = DateTime.Now;
                if (write)
                {
                    linkCache.WriteTime = DateTime.Now;
                }
                _linkedList.AddFirst(linkCache);
            }
        }

        public Tuple<V,DateTime,DateTime> Get(string key)
        {
            _locker.EnterUpgradeableReadLock();
            try
            {
                DateTime readTime = DateTime.MinValue;
                DateTime writeTime = DateTime.MinValue;
                var v = cache.Get(key);
                var cacheData = _linkedList.FirstOrDefault(p => p.Key == key);
                var pass = v != null && cacheData != null;
                if (!pass)
                {
                    return new Tuple<V, DateTime, DateTime>(default(V), readTime, writeTime);
                }

                readTime = cacheData.AccessTime;
                writeTime = cacheData.WriteTime;
                _locker.EnterWriteLock();
                try
                {
                    LRUReSort(cacheData);
                }
                finally { _locker.ExitWriteLock(); }
                return new Tuple<V, DateTime, DateTime>((V)v, readTime, writeTime);
            }
            catch { throw; }
            finally { _locker.ExitUpgradeableReadLock(); }
        }

        public bool Remove(string key)
        {
            return cache.Remove(key) != null;
        }

        public bool ContainsKey(string key)
        {
            _locker.EnterReadLock();
            try
            {
                return cache.Contains(key);
            }
            finally { _locker.ExitReadLock(); }
        }

        public long Count
        {
            get
            {
                _locker.EnterReadLock();
                try
                {
                    return cache.GetCount();
                }
                finally { _locker.ExitReadLock(); }
            }
        }

        public long Capacity
        {
            get
            {
                _locker.EnterReadLock();
                try
                {
                    return _capacity;
                }
                finally { _locker.ExitReadLock(); }
            }
            set
            {
                _locker.EnterUpgradeableReadLock();
                try
                {
                    if (value > 0 && _capacity != value)
                    {
                        _locker.EnterWriteLock();
                        try
                        {
                            _capacity = value;
                            while (_linkedList.Count > _capacity)
                            {
                                var last = _linkedList.LastOrDefault();
                                if (last != null)
                                {
                                    Remove(last.Key);
                                }
                            }
                        }
                        finally { _locker.ExitWriteLock(); }
                    }
                }
                finally { _locker.ExitUpgradeableReadLock(); }
            }
        }

        public ICollection<string> Keys
        {
            get
            {
                _locker.EnterReadLock();
                try
                {
                    return  GetCacheKeys();
                }
                finally { _locker.ExitReadLock(); }
            }
        }

        /// <summary>
        /// 获取所有缓存键
        /// </summary>
        /// <returns></returns>
        private List<string> GetCacheKeys()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var entries = cache.GetType().GetField("_entries", flags).GetValue(cache);
            var cacheItems = entries as IDictionary;
            var keys = new List<string>();
            if (cacheItems == null)
            {
                return keys;
            }
            foreach (DictionaryEntry cacheItem in cacheItems)
            {
                keys.Add(cacheItem.Key.ToString());
            }
            return keys;
        }

        /// <summary>
        /// 缓存删除后处理
        /// </summary>
        /// <param name="arguments"></param>
        private void AfterCacheRemove(CacheEntryRemovedArguments arguments)
        {
            var key = arguments.CacheItem.Key;
            if (!this.cache.Contains(key))
            {
                lock (_linkedList)
                {
                    var linkCache = _linkedList.FirstOrDefault(p => p.Key == key);
                    if (linkCache != null)
                    {
                        _linkedList.Remove(linkCache);
                    }
                }
            }
        }

        ///// <summary>
        ///// 重新分配大小
        ///// </summary>
        //private void ReSize()
        //{
        //    //内存分配
        //    if (_size > _totalSize)
        //    {
        //        if (_maxSize <= 0)
        //        {
        //            //未分配最大容量，自动重新分配，按照_capacity倍数分配
        //            _totalSize += _capacity;
        //            _size++;
        //        }
        //        else
        //        {
        //            //分配了最大值，校验是否溢出，溢出则淘汰
        //            var canReSize = _maxSize - this._totalSize; //剩余可分配大小
        //            if (canReSize > 0)
        //            {
        //                //可以分配，继续分配
        //                _totalSize += canReSize > _capacity ? _capacity : canReSize;
        //                _size++;
        //            }
        //            else
        //            {
        //                //不能分配了，执行淘汰策略
        //                // 如果更新节点后超出容量，删除最后一个
        //                var last = this._linkedList.LastOrDefault();
        //                if (last != null)
        //                {
        //                    cache.Remove(last.Key);
        //                    _size--;
        //                }
        //            }
        //        }
        //    }
        //}
    }
}
