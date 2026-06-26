-- [VSR] ACTION / TASK / DO: move a scene object in World space with Polytoria TweenPosition.
do
	local target = ${targetExpression}
	if target == nil then
${fallbackBlock}
	else
		local moveOffset = ${moveOffset}
		local tween = Tween:NewTween()
		tween:SetTrans(${transitionExpression})
		tween:SetDirection(${directionExpression})
		tween:TweenPosition(target, target.Position + moveOffset, ${duration})
${tweenCompletionBlock}
	end
end
