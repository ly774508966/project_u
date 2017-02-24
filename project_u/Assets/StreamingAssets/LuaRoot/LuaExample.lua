local UnityDebug = csharp.import("UnityEngine.Debug, UnityEngine")
local Vector3 = csharp.import('UnityEngine.Vector3, UnityEngine')
local Color = csharp.import('UnityEngine.Color, UnityEngine')
local LuaExample = {
	staticValue0 = 52
}

function LuaExample._Init(instance)
	instance.value0 = 32
	instance.value1 = 'hello'
	instance.value2 = Vector3(1, 2, 3)
	instance.color = Color.white;
	instance.newColor = Color.red;
	instance.anotherValue = Vector3(4, 5, 6)
	instance.anotherValue2 = Vector3(4, 5, 6)
	instance.anotherValue5 = Vector3(4, 5, 6)
	instance.anotherValue6 = Vector3(4, 5, 6)
	instance.anotherValue7 = Vector3(4, 5, 6)
end

function LuaExample:Awake()
	UnityDebug.Log(self.value0)
	UnityDebug.Log(self.value1)
	UnityDebug.Log(42)
	self.value0 = 'test'
	UnityDebug.Log(self.value0)
	UnityDebug.Log(LuaExample.value0)
	UnityDebug.Log('staticValue0 = '..self.staticValue0)
	local obj = self:FindGameObject('MyObject')
	if obj then
		UnityDebug.Log('Found object with key MyObject, it named ' .. obj.name)
	else
		UnityDebug.Log('Cannot find object with key MyObject')
	end
end

function LuaExample:Update()
	-- UnityDebug.Log("LuaExample.Update")
	-- self:InnerFunc();
	-- self:Test()
	-- self.StaticTest()
	-- self:TestWithParam("Hello World!")
	-- self:SendMessage("MessageFromLua")
	-- UnityDebug.Log("Update in Lua")
end

return LuaExample
