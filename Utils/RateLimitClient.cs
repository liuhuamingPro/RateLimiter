using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Utils
{
    public class RateLimitClient
    {
        private readonly IDatabase _db;

        public RateLimitClient(RedisHelper client)
        {
            _db = client.GetDatabase();
        }

        #region 分布式限流令牌桶算法
        /// <summary>
        /// 初始化令牌桶-hash结构
        /// </summary>
        /// <param name="key">令牌桶的key</param>
        /// <param name="bucketSize">桶大小</param>
        /// <param name="rate">向令牌桶中添加令牌的速率  , 令牌消耗速率(/s)</param>
        /// <param name="app">定义标记，比如哪些是被限流的</param>
        /// <returns></returns>
        public async Task<bool> InitBucket(string key, int bucketSize, int rate, string app)
        {
            bool token = false;
            StringBuilder sb = new StringBuilder();
            sb.Append(" local result=1 ");
            sb.Append(" redis.pcall('HMSET',@key, ");
            sb.Append(" 'last_mill_second',@last_mill_second, ");
            sb.Append(" 'curr_permits',@curr_permits, ");
            sb.Append(" 'max_burst',@max_burst, ");
            sb.Append(" 'rate',@rate, ");
            sb.Append(" 'app',@app) ");
            sb.Append(" return result ");

            long currMillSecond = TimeHelper.DateTimeToUnixTimestampMs(DateTime.Now);
            string last_mill_second = currMillSecond.ToString();//上一次添加令牌的毫秒数
            string curr_permits = "3";//令牌桶的最少令牌数
            var result = await _db.ScriptEvaluateAsync(LuaScript.Prepare(sb.ToString()), new { key = "ratelimit:" + key, last_mill_second, curr_permits, max_burst = bucketSize.ToString(), rate = rate.ToString(), app });
            if (result.ToString() == "1")
            {
                token = true;
            }
            else if (result.ToString() == "0")
            {
                token = true;
            }
            else
            {
                token = false;
            }
            return token;


        }

        /// <summary>
        /// 获取token
        /// </summary>
        /// <param name="key">令牌桶key</param>
        /// <param name="permits">每次请求令牌数</param>
        /// <returns></returns>
        public async Task<bool> AccquireToken(string key, int permits)
        {
            bool token = false;
            StringBuilder sb = new StringBuilder();
            sb.Append(" local ratelimit_info=redis.pcall('HMGET',@key, ");
            sb.Append(" 'last_mill_second','curr_permits','max_burst','rate','app') ");
            sb.Append(" local last_mill_second = ratelimit_info[1] ");
            sb.Append(" local curr_permits = tonumber(ratelimit_info[2]) ");
            sb.Append(" local max_burst = tonumber(ratelimit_info[3]) ");
            sb.Append(" local rate=tonumber(ratelimit_info[4]) ");
            sb.Append(" local app=tostring(ratelimit_info[5]) ");
            sb.Append(" if app == nil then ");
            sb.Append(" return 0 end ");
            sb.Append(" local local_curr_permits = max_burst; ");
            sb.Append(" if (type(last_mill_second) ~= 'boolean' and last_mill_second ~= nil) then ");
            sb.Append(" local reverse_permits=math.floor((@currMillSecond-last_mill_second)/1000)*rate ");
            sb.Append(" if(reverse_permits>0) then ");
            sb.Append(" redis.pcall('HMSET',@key,'last_mill_second',@currMillSecond) end ");
            sb.Append(" local expect_curr_permits=reverse_permits+curr_permits ");
            sb.Append(" local_curr_permits=math.min(expect_curr_permits,max_burst); ");
            sb.Append(" else ");
            sb.Append(" redis.pcall('HMSET',@key,'last_mill_second',@currMillSecond) end ");
            sb.Append(" local result = -1 ");
            sb.Append(" if (local_curr_permits - @permits > 0) then ");
            sb.Append(" result=1 redis.pcall('HMSET', @key, 'curr_permits', local_curr_permits - @permits) ");
            sb.Append(" else ");
            sb.Append(" redis.pcall('HMSET',@key,'curr_permits',local_curr_permits) end ");
            sb.Append(" return result ");
            string currMillSecond = TimeHelper.DateTimeToUnixTimestampMs(DateTime.Now).ToString();
            var result = await _db.ScriptEvaluateAsync(LuaScript.Prepare(sb.ToString()), new { key = "ratelimit:" + key, permits = permits.ToString(), currMillSecond });
            if (result.ToString() == "1")
            {
                token = true;
            }
            else
            {
                token = false;
            }
            return token;
        }
        #endregion

        /// <summary>
        /// 计数器算法
        /// </summary>
        /// <param name="expireTime">失效时间，如过了这个时间，计数器自动清0</param>
        /// <param name="key">限流的目标</param>
        /// <returns></returns>
        public async Task<long> Counter(int expireTime, string key)
        {
            string script = "" +
               "local current " +
               "local expire_time = @expireTime " +
               " current = redis.call('incr', @key) " +
               "if tonumber(current) == 1 then " +
                     "redis.call('expire', @key, expire_time)" +
              " end " +
               "return current";
            var result = await _db.ScriptEvaluateAsync(LuaScript.Prepare(script), new { expireTime, key });
            long count = Convert.ToInt64(result.ToString());
            return count;
        }

    }
}
