using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoadingCache
{
    public class CacheBuilder<V> //where V : CacheDataTime
    {
        #region * filed
        /// <summary>
        /// 默认值
        /// </summary>
        public const int UNSET_INT = -1;
        private const int DEFAULT_INITIAL_CAPACITY = 16;
        //private const int DEFAULT_CONCURRENCY_LEVEL = 4;
        private const int DEFAULT_EXPIRATION_MILLISECONDS = 0;
        private const int DEFAULT_REFRESH_MILLISECONDS = 0;

        private int initCapacity;
        private long maxImunSize;
        private long expireAfterAccessMilliseconds;
        private long expireAfterWriteMilliseconds;
        private long refreshAfterWriteMilliseconds;
        private long refreshMilliseconds;
        private bool autoReSize = false;
        private bool autoRefresh = false;

        /// <summary>
        /// 初始化容量
        /// </summary>
        public int InitCapacity
        {
            get
            {
                return initCapacity == UNSET_INT ? DEFAULT_INITIAL_CAPACITY : initCapacity;
            }
        }

        /// <summary>
        /// 最大容量
        /// </summary>
        public long MaxImumSize
        {
            get
            {
                return maxImunSize;
            }
        }

        /// <summary>
        /// 当缓存项在指定的时间段内没有被读或写就会被回收
        /// </summary>
        public long ExpireAfterAccessMilliseconds
        {
            get
            {
                return expireAfterAccessMilliseconds == UNSET_INT ? DEFAULT_EXPIRATION_MILLISECONDS : expireAfterAccessMilliseconds;
            }
        }

        /// <summary>
        /// 当缓存项在指定的时间段内没有更新就会被回收
        /// </summary>
        public long ExpireAfterWriteMilliseconds
        {
            get
            {
                return expireAfterWriteMilliseconds == UNSET_INT ? DEFAULT_EXPIRATION_MILLISECONDS : expireAfterWriteMilliseconds;
            }
        }

        /// <summary>
        /// 当缓存项上一次更新操作之后的多久会被刷新
        /// (只有一个线程协助刷新缓存，不阻塞其他线程)
        /// </summary>
        /// <returns></returns>
        public long RefreshAfterWriteMilliseconds
        {
            get
            {
               return this.refreshAfterWriteMilliseconds == UNSET_INT ? DEFAULT_EXPIRATION_MILLISECONDS : refreshAfterWriteMilliseconds;
            }
        }

        ///// <summary>
        ///// 当缓存项上一次更新操作之后的多久会被刷新
        ///// （超时触发自动刷新缓存机制，该值建议小于缓存时间）
        ///// </summary>
        //public long RefreshMilliseconds
        //{
        //    get
        //    {
        //        return refreshMilliseconds == UNSET_INT ? DEFAULT_REFRESH_MILLISECONDS : refreshMilliseconds;
        //    }
        //}

        /// <summary>
        /// 自动根据内存大小调整
        /// </summary>
        public bool AutoResize { get { return autoReSize; } }

        /// <summary>
        /// 自动缓存更新
        /// </summary>
        public bool AutoRefresh { get { return autoRefresh; } }
        #endregion

        /// <summary>
        /// 私有化构造函数
        /// </summary>
        private CacheBuilder()
        {
            initCapacity = UNSET_INT;
            maxImunSize = UNSET_INT;
            expireAfterAccessMilliseconds = UNSET_INT;
            expireAfterWriteMilliseconds = UNSET_INT;
            refreshMilliseconds = UNSET_INT;
            refreshAfterWriteMilliseconds = UNSET_INT;
        }

        /// <summary>
        /// 创建对象
        /// </summary>
        /// <returns></returns>
        public static CacheBuilder<V> NewBuilder()
        {
            return new CacheBuilder<V>();
        }

        /// <summary>
        /// 设置最大容量(注意这是缓存个数)
        /// </summary>
        /// <param name="maxImumSize"></param>
        /// <returns></returns>
        public CacheBuilder<V> SetMaxImumSize(long maxImumSize)
        {
            this.maxImunSize = maxImumSize;
            return this;
        }

        /// <summary>
        /// 当缓存项在指定的时间段内没有被读或写就会被回收
        /// (设置缓存有效时间后意味着缓存自动失效，数据加载时会阻塞线程)
        /// </summary>
        /// <param name="expireAfterAccess"></param>
        /// <returns></returns>
        public CacheBuilder<V> SetExpireAfterAccessMilliseconds(long expireAfterAccess)
        {
            this.expireAfterAccessMilliseconds = expireAfterAccess;
            return this;
        }

        /// <summary>
        /// 当缓存项在指定的时间段内没有更新就会被回收
        /// </summary>
        /// <param name="expireAfterWriteNanos"></param>
        /// <returns></returns>
        public CacheBuilder<V> SetExpireAfterWriteMilliseconds(long expireAfterWriteNanos)
        {
            this.expireAfterWriteMilliseconds = expireAfterWriteNanos;
            return this;
        }

        /// <summary>
        /// 当缓存项上一次更新操作之后的多久会被刷新
        /// (只有一个线程协助刷新缓存，不阻塞其他线程，值应该比ExpireAfterAccess小)
        /// </summary>
        /// <param name="refreshAfterWriteMilliseconds"></param>
        /// <returns></returns>
        public CacheBuilder<V> SetRefreshAfterWriteMilliseconds(long refreshAfterWriteMilliseconds)
        {
            this.refreshAfterWriteMilliseconds = refreshAfterWriteMilliseconds;
            return this;
        }

        ///// <summary>
        ///// 设置刷新时间
        ///// (只有一个线程协助刷新缓存，不阻塞其他线程)
        ///// </summary>
        ///// <param name="refreshNanos"></param>
        ///// <returns></returns>
        //public CacheBuilder<V> SetRefreshMilliseconds(long refreshNanos)
        //{
        //    this.refreshMilliseconds = refreshNanos;
        //    return this;
        //}

        /// <summary>
        /// 根据内存大小自动调整 
        /// </summary>
        /// <param name="autoReSize"></param>
        /// <returns></returns>
        public CacheBuilder<V> SetAutoResize(bool autoReSize)
        {
            this.autoReSize = autoReSize;
            return this;
        }

        /// <summary>
        /// 自动更新缓存
        /// （后台更新）
        /// </summary>
        /// <param name="autoReSize"></param>
        /// <returns></returns>
        public CacheBuilder<V> SetAutoRefresh(bool autoRefresh)
        {
            this.autoRefresh = autoRefresh;
            return this;
        }

        /// <summary>
        /// 构建缓存对象
        /// </summary>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public LoadingCache<V> Build(Func<string, V> func)
        {
            return new LoadingCache<V>(this, func);
        }

        ///// <summary>
        ///// 构建缓存对象
        ///// </summary>
        ///// <param name="key"></param>
        ///// <param name="func"></param>
        ///// <returns></returns>
        //public LoadingCache<V> Build(CacheLoader<V> cacheLoader)
        //{
        //    return new LoadingCache<V>(this, cacheLoader);
        //}
    }
}
