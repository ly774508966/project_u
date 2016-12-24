local ClickMe = {}
local Button = csharp.import('UnityEngine.UI.Button, UnityEngine.UI')
local Debug = csharp.import('UnityEngine.Debug, UnityEngine')

function ClickMe:Awake(instance)
	local btn = self:GetComponent(Button)
	btn.onClick:AddListener(
		function()
			Debug.Log('ClickMe from Lua!')
		end)
end

return ClickMe
