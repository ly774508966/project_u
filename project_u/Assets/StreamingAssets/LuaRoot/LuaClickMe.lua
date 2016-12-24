local ClickMe = {}
local Button = csharp.import('UnityEngine.UI.Button, UnityEngine.UI')

function ClickMe:Awake(instance)
	instance.btn = self:GetComponent(Button)
end

return ClickMe
