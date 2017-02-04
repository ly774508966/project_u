local ClickMe = {}
local Button = csharp.checked_import('UnityEngine.UI.Button, UnityEngine.UI')
local Debug = csharp.checked_import('UnityEngine.Debug, UnityEngine')

function ClickMe._Init(instance)
    instance.value = 10
end

function ClickMe:Awake(instance)
--[[
	local btn = self:GetComponent(Button)
	btn.onClick:AddListener(
		function()
			Debug.Log('ClickMe from Lua!')
		end)
--]]
end

function ClickMe:OnClick(instance)
	Debug.Log('OnClick in Lua ' .. instance.value)
	local val = instance.value
	local co = coroutine.create(
		function() 
			local WaitForSeconds = csharp.checked_import('UnityEngine.WaitForSeconds, UnityEngine')
			while val >= 0 do
--				Debug.Log('in coroutine')
				val = val - 1
				coroutine.yield(WaitForSeconds(1))
			end
		end)
	Debug.Log(type(co))
	self:StartLuaCoroutine(co)
end

return ClickMe
