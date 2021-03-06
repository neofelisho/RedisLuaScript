﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace RedisLuaScript
{
    internal class Program
    {
#if DEBUG
        private static readonly string BasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}");
        private static readonly IConfiguration Configuration =
            new ConfigurationBuilder().SetBasePath(BasePath).AddJsonFile("appsettings.Debug.json").Build();
#else
        private static readonly string BasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
        private static readonly IConfiguration Configuration =
            new ConfigurationBuilder().SetBasePath(BasePath).AddJsonFile("appsettings.json").Build();
#endif
        private static readonly Lazy<IConnectionMultiplexer> Connection =
            new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(Configuration["RedisConnection"]));

        private static void Main()
        {
            Console.WriteLine("--- Begin the Redis Lua Script testing ---");
            Console.WriteLine("[1] Test atomic operation.");
            Console.WriteLine("[2] Test script of getting available server.");
            Console.WriteLine("[3] Test script of getting serial number cross multiple host.");
            Console.WriteLine("[4] Test script of getting sms verify code.");
            Console.WriteLine("[5] Test script of rollback sms verify code.");

            var input = Console.ReadKey();
            Console.WriteLine();
            if (!int.TryParse(input.KeyChar.ToString(), out var choice)) return;

            switch (choice)
            {
                case 1:
                    TestAtomicOperation();
                    break;
                case 2:
                    TestGetAvailableServer();
                    break;
                case 3:
                    TestGetSerialCrossHost();
                    break;
                case 4:
                    TestGetSmsVerifyCode();
                    break;
                case 5:
                    TestRollbackSmsVerifyCode();
                    break;
                default:
                    Console.WriteLine($"No test be executed.");
                    break;
            }

            Console.Read();
        }

        private static void TestRollbackSmsVerifyCode()
        {
            var luaScript = GetLuaScript("SmsVerifyCode.lua");
            var keys = GetSmsKeys();
            var argv = GetSmsArgs();
            var redis = Connection.Value.GetDatabase(7);
            redis.ScriptEvaluate(luaScript, keys, argv);
                Console.WriteLine("Complete the setting of sms verify code...");
                Console.Read();
            luaScript = GetLuaScript("RollbackVerifyCode.lua");
            redis.ScriptEvaluate(luaScript, keys);
        }

        private static void TestGetSmsVerifyCode()
        {
            var luaScript = GetLuaScript("SmsVerifyCode.lua");
            var keys = GetSmsKeys();
            var argv = GetSmsArgs();

            var redis = Connection.Value.GetDatabase(7);

            var tasks = Enumerable.Range(0, 300).Select(_ =>
                Task.Run(async () => Console.WriteLine(await redis.ScriptEvaluateAsync(luaScript, keys, argv))));
            Task.WaitAll(tasks.ToArray());

            Console.WriteLine("Press any key to close.");
        }

        private static RedisValue[] GetSmsArgs()
        {
            RedisValue[] argv =
                        {
                "EXPIRE",
                3600,
                20,
                300,
                1,
                600,
                "9487",
                "0987987987"
            };
            return argv;
        }

        private static RedisKey[] GetSmsKeys()
        {
            return new RedisKey[] { "TestSMS:DailyCount", "TestSMS:Retry", "TestSMS:VerifyCode" };
        }

        private static void TestGetSerialCrossHost()
        {
            const int limitCount = 10000;
            var resetTime = ((DateTimeOffset) DateTime.UtcNow.Date.AddDays(1)).ToUnixTimeSeconds();

            var luaScript = $@"
local result = redis.call('INCR', KEYS[1])
if result == 1 then
	redis.call('EXPIREAT', KEYS[1], {resetTime})
end
if result > {limitCount} then
	result = -1
end
return result";

            var redisKey = new RedisKey[] { "TestXSerial" };
            var redis = Connection.Value.GetDatabase(7);

            var tasks = Enumerable.Range(0, 3000).Select(_ =>
                Task.Run(async () => Console.WriteLine(await redis.ScriptEvaluateAsync(luaScript, redisKey))));
            Task.WaitAll(tasks.ToArray());
            //redis.KeyDelete(redisKey);

            Console.WriteLine("Press any key to close.");
        }

        private static void TestGetAvailableServer()
        {
            var luaScript = GetLuaScript("GetServer.lua");
            var servers = new RedisKey[] {"8", "9", "10"};

            var tasks = Enumerable.Range(0, servers.Length * 3).AsParallel().Select(p =>
                Task.Run(async () =>
                {
                    var redis = Connection.Value.GetDatabase(7);
                    var result = await redis.ScriptEvaluateAsync(luaScript, servers);
                    Console.WriteLine(result.IsNull
                        ? $"There is no available server for process {p}."
                        : $"Get available server: {result} for process {p}.");
                })
            ).ToArray();
            Task.WaitAll(tasks);
            Console.WriteLine($"Press any key to close.");
        }

        private static void TestAtomicOperation()
        {
            var luaScript = GetLuaScript("AtomicOperation.lua");

            var redisKey = new RedisKey[] {"AtomicOperation"};
            var redis = Connection.Value.GetDatabase(7);

            var tasks = Enumerable.Range(0, 30).Select(_ =>
                Task.Run(async () => Console.WriteLine(await redis.ScriptEvaluateAsync(luaScript, redisKey))));
            Task.WaitAll(tasks.ToArray());
            redis.KeyDelete(redisKey);
            Console.WriteLine($"Press any key to close.");
        }

        private static string GetLuaScript(string fileName)
        {
            var luaPath = Path.Combine(
                BasePath,
                fileName);
            return File.ReadAllText(luaPath);
        }
    }
}