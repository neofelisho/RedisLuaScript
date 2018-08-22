local result = redis.call('INCR', KEYS[1])
if result > 100 then
	redis.call('DEL', KEYS[1])
	result = redis.call('INCR', KEYS[1])
end
return result