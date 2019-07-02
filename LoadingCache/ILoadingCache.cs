using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoadingCache
{
    public interface ILoadingCache<T>
    {
        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        T GetIfPresent(string key);

        /// <summary>
        /// 获取数据，缓存没有则从委托查询
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        T Get(string key);

        /// <summary>
        /// 添加缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void Set(string key, T value);

        /// <summary>
        /// 获取所有有效缓存
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        Dictionary<string, T> GetAllPresent(List<string> keys);

        /// <summary>
        /// 是缓存失效
        /// </summary>
        /// <param name="key"></param>
        void Invalidate(string key);
    }
}
