local softcap = redis.call('INCR', KEYS[2])
if softcap > 1 then
	local softttl = redis.call('TTL', KEYS[2])
	local availablettl = redis.call('TTL', KEYS[3])
    return '-2'..':'..ARGV[8]..':'..softttl..':'..availablettl
end
local hardcap = redis.call('INCR', KEYS[1])
if hardcap > tonumber(ARGV[3]) then
	local hardttl = redis.call('TTL', KEYS[1])
	local availablettl = redis.call('TTL', KEYS[3])
	return '-1'..':'..ARGV[8]..':'..hardttl..':'..availablettl
end
if hardcap == 1 then
	redis.call(ARGV[1], KEYS[1], ARGV[2])
end
if softcap == tonumber(ARGV[5]) then
    redis.call('EXPIRE', KEYS[2], ARGV[4])
end
redis.call('SET', KEYS[3], ARGV[7]..':'..ARGV[8])
redis.call('EXPIRE', KEYS[3], ARGV[6])
local softttl = redis.call('TTL', KEYS[2])
return ARGV[7]..':'..ARGV[8]..':'..softttl..':'..ARGV[6]
