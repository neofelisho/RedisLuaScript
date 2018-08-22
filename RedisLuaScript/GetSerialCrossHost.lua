local result = redis.call('INCR', KEYS[1])
if result == 1 then
	redis.call('EXPIRE', KEYS[1], 60)
end
if result > 10000 then
	result = -1
end
return result