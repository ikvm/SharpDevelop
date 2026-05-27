using System;
using System.Diagnostics.Contracts;

namespace ICSharpCode.NRefactory.TypeSystem
{
	/// <summary>
	/// 继承自 Abstractions 中的 INamedElement，使 NRefactory 类型同时满足两个接口。
	/// 所有成员类型相同（string），完全继承。
	/// </summary>
	public interface INamedElement : ICSharpCode.TypeSystem.INamedElement
	{
	}
}
