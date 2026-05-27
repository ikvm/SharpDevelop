using System;
using System.Collections;
using System.Collections.Generic;

namespace ICSharpCode.NRefactory.TypeSystem.Implementation
{
	/// <summary>
	/// 将 IList{TSource} 适配为 IList{TTarget} 的简单包装器。
	/// 用于 NRefactory 接口继承 Abstractions 接口时的显式接口实现。
	/// 假设每个 TSource 元素都可以安全地转换为 TTarget。
	/// </summary>
	internal class CastList<TSource, TTarget> : IList<TTarget> where TSource : TTarget
	{
		readonly IList<TSource> source;
		
		public CastList(IList<TSource> source)
		{
			this.source = source ?? throw new ArgumentNullException(nameof(source));
		}
		
		public TTarget this[int index] {
			get => source[index];
			set => throw new NotSupportedException();
		}
		
		public int Count => source.Count;
		public bool IsReadOnly => true;
		
		public void Add(TTarget item) => throw new NotSupportedException();
		public void Clear() => throw new NotSupportedException();
		public bool Contains(TTarget item) => source.Contains((TSource)item);
		public void CopyTo(TTarget[] array, int arrayIndex) {
			for (int i = 0; i < source.Count; i++)
				array[arrayIndex + i] = source[i];
		}
		public IEnumerator<TTarget> GetEnumerator() {
			foreach (var item in source)
				yield return item;
		}
		public int IndexOf(TTarget item) => source.IndexOf((TSource)item);
		public void Insert(int index, TTarget item) => throw new NotSupportedException();
		public bool Remove(TTarget item) => throw new NotSupportedException();
		public void RemoveAt(int index) => throw new NotSupportedException();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
