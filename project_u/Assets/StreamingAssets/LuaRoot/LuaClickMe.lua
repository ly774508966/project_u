local ClickMe = {}
local Button = csharp.import('UnityEngine.UI.Button, UnityEngine.UI')
local Debug = csharp.import('UnityEngine.Debug, UnityEngine')

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
	Debug.Log("OnClick in Lua" .. instance.value)
end

return ClickMe
