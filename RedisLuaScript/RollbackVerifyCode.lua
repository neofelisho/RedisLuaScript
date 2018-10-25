redis.call('DECR', KEYS[1])
redis.call('DECR', KEYS[2])
redis.call('DEL', KEYS[3])