local retrycap = redis.call('INCR', KEYS[2])
if retrycap > 1 then
    return -2
end
local dailycap = redis.call('INCR', KEYS[1])
if dailycap > 20 then
	return -1
end
if dailycap == 1 then
    redis.call('EXPIREAT', KEYS[1], {accumulatedCountExpireAt})
end
if retrycap == 1 then
    redis.call('EXPIRE', KEYS[2], 300)
end
local code = redis.call('GET', KEYS[3])
if code then 
    return code
end
redis.call('SET', KEYS[3], '9487')
redis.call('EXPIRE', KEYS[3], 600)
return '9487'