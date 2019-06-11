using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Utils;

namespace LimitRateApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {

        private readonly IDatabase _db;
        private readonly RateLimitClient _rateLimitClient;

        public ValuesController(RedisHelper client, RateLimitClient rateLimitClient)
        {
            _db = client.GetDatabase();
            _rateLimitClient = rateLimitClient;
        }


        [HttpGet]
        public async Task<bool> Get()
        {
            string key = "first";
            bool res = await _rateLimitClient.AccquireToken(key, 1);
            if (res)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        [Route("InitBucket")]
        [HttpGet]
        public async Task<bool> InitBucket()
        {
            string key = "first";
            int bucketSize = 5;
            int rate = 1;
            string app = "app";
            return await _rateLimitClient.InitBucket(key, bucketSize, rate, app);
        }

        /// <summary>
        /// 计数器算法
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<string> Get(int id)
        {
            string key = "ip";
            var count = await _rateLimitClient.Counter(60, key);
            if (count > 10)
            {

                return "你被限流";
            }
            else
            {
                return $"访问{count}次";
            }
        }


    }
}
