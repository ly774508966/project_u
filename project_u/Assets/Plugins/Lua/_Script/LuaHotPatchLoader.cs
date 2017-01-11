using UnityEngine;
using System.Collections;
using System;
using System.Reflection;
using System.Linq.Expressions;

namespace lua.hotpatch
{
	public class LuaHotPatchLoader
	{

		static MemberInfo MemberInfoCore(Expression body, ParameterExpression param)
		{
			if (body.NodeType == ExpressionType.MemberAccess)
			{
				var bodyMemberAccess = (MemberExpression)body;
				return bodyMemberAccess.Member;
			}
			else if (body.NodeType == ExpressionType.Call)
			{
				var bodyMemberAccess = (MethodCallExpression)body;
				return bodyMemberAccess.Method;
			}
			else throw new NotSupportedException();
		}

		static MemberInfo MemberInfo<T1>(Expression<Func<T1>> memberSelectionExpression)
		{
			if (memberSelectionExpression == null) throw new ArgumentNullException("memberSelectionExpression");
			return MemberInfoCore(memberSelectionExpression.Body, null/*param*/);
		}


		[LuaHotPatchHub]
		public static void Hub(int a)
		{
			Debug.Log("DDDDDD");
		}

		static object Hub(System.Reflection.MethodBase method, object target, params object[] args)
		{
			try
			{
				return method.Invoke(target, args);
			}
			catch (Exception e)
			{
				Debug.LogError("Catch " + e.InnerException.Message);
				throw e.InnerException;
			}
		}

	}
}
