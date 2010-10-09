﻿// Copyright (c) 2010 AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;

namespace ICSharpCode.NRefactory.CSharp.Resolver
{
	/// <summary>
	/// Contains the main resolver logic.
	/// </summary>
	public class CSharpResolver
	{
		static readonly ResolveResult ErrorResult = new ErrorResolveResult(SharedTypes.UnknownType);
		static readonly ResolveResult DynamicResult = new ResolveResult(SharedTypes.Dynamic);
		
		readonly ITypeResolveContext context;
		
		/// <summary>
		/// Gets/Sets whether the current context is <c>checked</c>.
		/// </summary>
		public bool IsCheckedContext { get; set; }
		
		public CSharpResolver(ITypeResolveContext context)
		{
			if (context == null)
				throw new ArgumentNullException("context");
			this.context = context;
		}
		
		#region class OperatorMethod
		static OperatorMethod[] Lift(params OperatorMethod[] methods)
		{
			List<OperatorMethod> result = new List<OperatorMethod>(methods);
			foreach (OperatorMethod method in methods) {
				OperatorMethod lifted = method.Lift();
				if (lifted != null)
					result.Add(lifted);
			}
			return result.ToArray();
		}
		
		class OperatorMethod : Immutable, IParameterizedMember
		{
			IList<IParameter> parameters = new List<IParameter>();
			
			public IList<IParameter> Parameters {
				get { return parameters; }
			}
			
			public ITypeReference ReturnType {
				get; set;
			}
			
			public virtual OperatorMethod Lift()
			{
				return null;
			}
			
			ITypeDefinition IEntity.DeclaringTypeDefinition {
				get { return null; }
			}
			
			ITypeDefinition IMember.DeclaringTypeDefinition {
				get { return null; }
			}
			
			IType IMember.DeclaringType {
				get { return null; }
			}
			
			IMember IMember.MemberDefinition {
				get { return null; }
			}
			
			IList<IExplicitInterfaceImplementation> IMember.InterfaceImplementations {
				get { return EmptyList<IExplicitInterfaceImplementation>.Instance; }
			}
			
			bool IMember.IsVirtual {
				get { return false; }
			}
			
			bool IMember.IsOverride {
				get { return false; }
			}
			
			bool IMember.IsOverridable {
				get { return false; }
			}
			
			EntityType IEntity.EntityType {
				get { return EntityType.Operator; }
			}
			
			DomRegion IEntity.Region {
				get { return DomRegion.Empty; }
			}
			
			DomRegion IEntity.BodyRegion {
				get { return DomRegion.Empty; }
			}
			
			IList<IAttribute> IEntity.Attributes {
				get { return EmptyList<IAttribute>.Instance; }
			}
			
			string IEntity.Documentation {
				get { return null; }
			}
			
			Accessibility IEntity.Accessibility {
				get { return Accessibility.Public; }
			}
			
			bool IEntity.IsStatic {
				get { return true; }
			}
			
			bool IEntity.IsAbstract {
				get { return false; }
			}
			
			bool IEntity.IsSealed {
				get { return false; }
			}
			
			bool IEntity.IsShadowing {
				get { return false; }
			}
			
			bool IEntity.IsSynthetic {
				get { return true; }
			}
			
			IProjectContent IEntity.ProjectContent {
				get { return null; }
			}
			
			string INamedElement.FullName {
				get { return "operator"; }
			}
			
			string INamedElement.Name {
				get { return "operator"; }
			}
			
			string INamedElement.Namespace {
				get { return string.Empty; }
			}
			
			string INamedElement.DotNetName {
				get { return "operator"; }
			}
			
			public override string ToString()
			{
				StringBuilder b = new StringBuilder();
				b.Append(ReturnType + " operator(");
				for (int i = 0; i < parameters.Count; i++) {
					if (i > 0)
						b.Append(", ");
					b.Append(parameters[i].Type);
				}
				b.Append(')');
				return b.ToString();
			}
		}
		#endregion
		
		#region ResolveUnaryOperator
		public ResolveResult ResolveUnaryOperator(UnaryOperatorType op, ResolveResult expression)
		{
			if (expression.Type == SharedTypes.Dynamic)
				return DynamicResult;
			
			// C# 4.0 spec: §7.3.3 Unary operator overload resolution
			string overloadableOperatorName = GetOverloadableOperatorName(op);
			if (overloadableOperatorName == null) {
				switch (op) {
					case UnaryOperatorType.Dereference:
						PointerType p = expression.Type as PointerType;
						if (p != null)
							return new ResolveResult(p.ElementType);
						else
							return ErrorResult;
					case UnaryOperatorType.AddressOf:
						return new ResolveResult(new PointerType(expression.Type));
					default:
						throw new ArgumentException("Invalid value for UnaryOperatorType", "op");
				}
			}
			// If the type is nullable, get the underlying type:
			IType type = NullableType.GetUnderlyingType(expression.Type);
			bool isNullable = NullableType.IsNullable(expression.Type);
			
			// the operator is overloadable:
			// TODO: implicit support for user operators
			//var candidateSet = GetUnaryOperatorCandidates();
			
			expression = UnaryNumericPromotion(op, ref type, isNullable, expression);
			OperatorMethod[] methodGroup;
			switch (op) {
				case UnaryOperatorType.Increment:
				case UnaryOperatorType.Decrement:
				case UnaryOperatorType.PostIncrement:
				case UnaryOperatorType.PostDecrement:
					// C# 4.0 spec: §7.6.9 Postfix increment and decrement operators
					// C# 4.0 spec: §7.7.5 Prefix increment and decrement operators
					TypeCode code = ReflectionHelper.GetTypeCode(type);
					if ((code >= TypeCode.SByte && code <= TypeCode.Decimal) || type.IsEnum())
						return new ResolveResult(expression.Type);
					else
						return new ErrorResolveResult(expression.Type);
				case UnaryOperatorType.Plus:
					methodGroup = unaryPlusOperators;
					break;
				case UnaryOperatorType.Minus:
					methodGroup = IsCheckedContext ? checkedUnaryMinusOperators : uncheckedUnaryMinusOperators;
					break;
				case UnaryOperatorType.Not:
					methodGroup = logicalNegationOperator;
					break;
				case UnaryOperatorType.BitNot:
					if (type.IsEnum()) {
						if (expression.IsCompileTimeConstant && !isNullable) {
							// evaluate as (E)(~(U)x);
							var U = expression.ConstantValue.GetType().ToTypeReference().Resolve(context);
							var unpackedEnum = new ConstantResolveResult(U, expression.ConstantValue);
							return CheckErrorAndResolveCast(expression.Type, ResolveUnaryOperator(op, unpackedEnum));
						} else {
							return new ResolveResult(expression.Type);
						}
					} else {
						methodGroup = bitwiseComplementOperators;
						break;
					}
				default:
					throw new InvalidOperationException();
			}
			OverloadResolution r = new OverloadResolution(context, new[] { expression });
			foreach (var candidate in methodGroup) {
				r.AddCandidate(candidate);
			}
			UnaryOperatorMethod m = (UnaryOperatorMethod)r.BestCandidate;
			IType resultType = m.ReturnType.Resolve(context);
			if (r.BestCandidateErrors != OverloadResolutionErrors.None) {
				return new ErrorResolveResult(resultType);
			} else if (expression.IsCompileTimeConstant && !isNullable) {
				object val;
				try {
					val = m.Invoke(this, expression.ConstantValue);
				} catch (ArithmeticException) {
					return new ErrorResolveResult(resultType);
				}
				return new ConstantResolveResult(resultType, val);
			} else {
				return new ResolveResult(resultType);
			}
		}
		
		ResolveResult UnaryNumericPromotion(UnaryOperatorType op, ref IType type, bool isNullable, ResolveResult expression)
		{
			// C# 4.0 spec: §7.3.6.1
			TypeCode code = ReflectionHelper.GetTypeCode(type);
			switch (op) {
				case UnaryOperatorType.Minus:
					if (code == TypeCode.UInt32) {
						IType targetType = context.GetClass(typeof(long)) ?? SharedTypes.UnknownType;
						type = targetType;
						if (isNullable) targetType = NullableType.Create(targetType, context);
						return ResolveCast(targetType, expression);
					}
					goto case UnaryOperatorType.Plus;
				case UnaryOperatorType.Plus:
				case UnaryOperatorType.BitNot:
					if (code >= TypeCode.Char && code <= TypeCode.UInt16) {
						IType targetType = context.GetClass(typeof(int)) ?? SharedTypes.UnknownType;
						type = targetType;
						if (isNullable) targetType = NullableType.Create(targetType, context);
						return ResolveCast(targetType, expression);
					}
					break;
			}
			return expression;
		}
		
		static string GetOverloadableOperatorName(UnaryOperatorType op)
		{
			switch (op) {
				case UnaryOperatorType.Not:
					return "op_LogicalNot";
				case UnaryOperatorType.BitNot:
					return "op_OnesComplement";
				case UnaryOperatorType.Minus:
					return "op_UnaryNegation";
				case UnaryOperatorType.Plus:
					return "op_UnaryPlus";
				case UnaryOperatorType.Increment:
				case UnaryOperatorType.PostIncrement:
					return "op_Increment";
				case UnaryOperatorType.Decrement:
				case UnaryOperatorType.PostDecrement:
					return "op_Decrement";
				default:
					return null;
			}
		}
		
		abstract class UnaryOperatorMethod : OperatorMethod
		{
			public abstract object Invoke(CSharpResolver resolver, object input);
		}
		
		sealed class LambdaUnaryOperatorMethod<T> : UnaryOperatorMethod
		{
			readonly Func<T, T> func;
			
			public LambdaUnaryOperatorMethod(Func<T, T> func)
			{
				this.ReturnType = typeof(T).ToTypeReference();
				this.Parameters.Add(new DefaultParameter(this.ReturnType, string.Empty));
				this.func = func;
			}
			
			public override object Invoke(CSharpResolver resolver, object input)
			{
				return func((T)resolver.CSharpPrimitiveCast(Type.GetTypeCode(typeof(T)), input));
			}
			
			public override OperatorMethod Lift()
			{
				return new LiftedUnaryOperatorMethod(this);
			}
		}
		
		sealed class LiftedUnaryOperatorMethod : UnaryOperatorMethod, OverloadResolution.ILiftedOperator
		{
			UnaryOperatorMethod baseMethod;
			
			public LiftedUnaryOperatorMethod(UnaryOperatorMethod baseMethod)
			{
				this.baseMethod = baseMethod;
				this.ReturnType = NullableType.Create(baseMethod.ReturnType);
				this.Parameters.Add(new DefaultParameter(NullableType.Create(baseMethod.Parameters[0].Type), string.Empty));
			}
			
			public override object Invoke(CSharpResolver resolver, object input)
			{
				if (input == null)
					return null;
				else
					return baseMethod.Invoke(resolver, input);
			}
			
			public IList<IParameter> NonLiftedParameters {
				get { return baseMethod.Parameters; }
			}
		}
		
		// C# 4.0 spec: §7.7.1 Unary plus operator
		static readonly OperatorMethod[] unaryPlusOperators = Lift(
			new LambdaUnaryOperatorMethod<int>(i => +i),
			new LambdaUnaryOperatorMethod<uint>(i => +i),
			new LambdaUnaryOperatorMethod<long>(i => +i),
			new LambdaUnaryOperatorMethod<ulong>(i => +i),
			new LambdaUnaryOperatorMethod<float>(i => +i),
			new LambdaUnaryOperatorMethod<double>(i => +i),
			new LambdaUnaryOperatorMethod<decimal>(i => +i)
		);
		
		// C# 4.0 spec: §7.7.2 Unary minus operator
		static readonly OperatorMethod[] uncheckedUnaryMinusOperators = Lift(
			new LambdaUnaryOperatorMethod<int>(i => unchecked(-i)),
			new LambdaUnaryOperatorMethod<long>(i => unchecked(-i)),
			new LambdaUnaryOperatorMethod<float>(i => -i),
			new LambdaUnaryOperatorMethod<double>(i => -i),
			new LambdaUnaryOperatorMethod<decimal>(i => -i)
		);
		static readonly OperatorMethod[] checkedUnaryMinusOperators = Lift(
			new LambdaUnaryOperatorMethod<int>(i => checked(-i)),
			new LambdaUnaryOperatorMethod<long>(i => checked(-i)),
			new LambdaUnaryOperatorMethod<float>(i => -i),
			new LambdaUnaryOperatorMethod<double>(i => -i),
			new LambdaUnaryOperatorMethod<decimal>(i => -i)
		);
		
		// C# 4.0 spec: §7.7.3 Logical negation operator
		static readonly OperatorMethod[] logicalNegationOperator = Lift(new LambdaUnaryOperatorMethod<bool>(b => !b));
		
		// C# 4.0 spec: §7.7.4 Bitwise complement operator
		static readonly OperatorMethod[] bitwiseComplementOperators = Lift(
			new LambdaUnaryOperatorMethod<int>(i => ~i),
			new LambdaUnaryOperatorMethod<uint>(i => ~i),
			new LambdaUnaryOperatorMethod<long>(i => ~i),
			new LambdaUnaryOperatorMethod<ulong>(i => ~i)
		);
		
		object GetUserUnaryOperatorCandidates()
		{
			// C# 4.0 spec: §7.3.5 Candidate user-defined operators
			// TODO: implement user-defined operators
			throw new NotImplementedException();
		}
		#endregion
		
		#region ResolveBinaryOperator
		public ResolveResult ResolveBinaryOperator(BinaryOperatorType op, ResolveResult lhs, ResolveResult rhs)
		{
			if (lhs.Type == SharedTypes.Dynamic || rhs.Type == SharedTypes.Dynamic)
				return DynamicResult;
			
			// C# 4.0 spec: §7.3.4 Binary operator overload resolution
			string overloadableOperatorName = GetOverloadableOperatorName(op);
			if (overloadableOperatorName == null) {
				switch (op) {
					case BinaryOperatorType.LogicalAnd:
						throw new NotImplementedException();
					case BinaryOperatorType.LogicalOr:
						throw new NotImplementedException();
					case BinaryOperatorType.NullCoalescing:
						throw new NotImplementedException();
					default:
						throw new ArgumentException("Invalid value for BinaryOperatorType", "op");
				}
			}
			
			// If the type is nullable, get the underlying type:
			bool isNullable = NullableType.IsNullable(lhs.Type) || NullableType.IsNullable(rhs.Type);
			IType lhsType = NullableType.GetUnderlyingType(lhs.Type);
			IType rhsType = NullableType.GetUnderlyingType(rhs.Type);
			
			// TODO: find user-defined operators
			if (!BinaryNumericPromotion(isNullable, ref lhs, ref rhs))
				return new ErrorResolveResult(lhs.Type);
			// re-read underlying types after numeric promotion
			lhsType = NullableType.GetUnderlyingType(lhs.Type);
			rhsType = NullableType.GetUnderlyingType(rhs.Type);
			
			IEnumerable<OperatorMethod> methodGroup;
			switch (op) {
				case BinaryOperatorType.Multiply:
					methodGroup = IsCheckedContext ? checkedMultiplicationOperators : uncheckedMultiplicationOperators;
					break;
				case BinaryOperatorType.Divide:
					methodGroup = IsCheckedContext ? checkedDivisionOperators : uncheckedDivisionOperators;
					break;
				case BinaryOperatorType.Modulus:
					methodGroup = IsCheckedContext ? checkedRemainderOperators : uncheckedRemainderOperators;
					break;
				case BinaryOperatorType.Add:
					methodGroup = IsCheckedContext ? checkedAdditionOperators : uncheckedAdditionOperators;
					Conversions conversions = new Conversions(context);
					if (lhsType.IsEnum() && conversions.ImplicitConversion(rhsType, lhsType.GetEnumUnderlyingType(context))) {
						// E operator +(E x, U y);
						if (lhs.IsCompileTimeConstant && rhs.IsCompileTimeConstant && !isNullable) {
							// evaluate as (E)((U)x + (U)y)
							lhs = ResolveCast(lhsType.GetEnumUnderlyingType(context), lhs);
							if (lhs.IsError) return lhs;
							rhs = ResolveCast(lhsType.GetEnumUnderlyingType(context), rhs);
							if (rhs.IsError) return rhs;
							return CheckErrorAndResolveCast(lhsType, ResolveBinaryOperator(op, lhs, rhs));
						}
						return new ResolveResult(isNullable ? NullableType.Create(lhsType, context) : lhsType);
					} else if (rhsType.IsEnum() && conversions.ImplicitConversion(lhsType, rhsType.GetEnumUnderlyingType(context))) {
						// E operator +(U x, E y);
						return ResolveBinaryOperator(op, rhs, lhs); // swap arguments
					}
					if (lhsType.IsDelegate() && conversions.ImplicitConversion(rhsType, lhsType)) {
						return new ResolveResult(lhsType);
					} else if (rhsType.IsDelegate() && conversions.ImplicitConversion(lhsType, rhsType)) {
						return new ResolveResult(rhsType);
					}
					if (lhsType == SharedTypes.Null && rhsType == SharedTypes.Null)
						return new ErrorResolveResult(SharedTypes.Null);
					break;
				default:
					throw new InvalidOperationException();
			}
			OverloadResolution r = new OverloadResolution(context, new[] { lhs, rhs });
			foreach (var candidate in methodGroup) {
				r.AddCandidate(candidate);
			}
			BinaryOperatorMethod m = (BinaryOperatorMethod)r.BestCandidate;
			IType resultType = m.ReturnType.Resolve(context);
			if (r.BestCandidateErrors != OverloadResolutionErrors.None) {
				return new ErrorResolveResult(resultType);
			} else if (lhs.IsCompileTimeConstant && rhs.IsCompileTimeConstant && !isNullable && m.CanEvaluateAtCompileTime) {
				object val;
				try {
					val = m.Invoke(this, lhs.ConstantValue, rhs.ConstantValue);
				} catch (ArithmeticException) {
					return new ErrorResolveResult(resultType);
				}
				return new ConstantResolveResult(resultType, val);
			} else {
				return new ResolveResult(resultType);
			}
		}
		
		bool BinaryNumericPromotion(bool isNullable, ref ResolveResult lhs, ref ResolveResult rhs)
		{
			// C# 4.0 spec: §7.3.6.2
			TypeCode lhsCode = ReflectionHelper.GetTypeCode(lhs.Type);
			TypeCode rhsCode = ReflectionHelper.GetTypeCode(rhs.Type);
			bool bindingError = false;
			if (lhsCode >= TypeCode.Char && lhsCode <= TypeCode.Decimal
			    && rhsCode >= TypeCode.Char && rhsCode <= TypeCode.Decimal)
			{
				Type targetType;
				if (lhsCode == TypeCode.Decimal || rhsCode == TypeCode.Decimal) {
					targetType = typeof(decimal);
					bindingError = (lhsCode == TypeCode.Single || lhsCode == TypeCode.Double
					                || rhsCode == TypeCode.Single || rhsCode == TypeCode.Double);
				} else if (lhsCode == TypeCode.Double || rhsCode == TypeCode.Double) {
					targetType = typeof(double);
				} else if (lhsCode == TypeCode.Single || rhsCode == TypeCode.Single) {
					targetType = typeof(float);
				} else if (lhsCode == TypeCode.UInt64 || rhsCode == TypeCode.UInt64) {
					targetType = typeof(ulong);
					bindingError = IsSigned(lhsCode) || IsSigned(rhsCode);
				} else if (lhsCode == TypeCode.Int64 || rhsCode == TypeCode.Int64) {
					targetType = typeof(long);
				} else if (lhsCode == TypeCode.UInt32 || rhsCode == TypeCode.UInt32) {
					targetType = (IsSigned(lhsCode) || IsSigned(rhsCode)) ? typeof(long) : typeof(uint);
				} else {
					targetType = typeof(int);
				}
				lhs = CastTo(targetType, isNullable, lhs);
				rhs = CastTo(targetType, isNullable, rhs);
			}
			return !bindingError;
		}
		
		bool IsSigned(TypeCode code)
		{
			return code == TypeCode.SByte || code == TypeCode.Int16 || code == TypeCode.Int32 || code == TypeCode.Int64;
		}
		
		ResolveResult CastTo(Type targetType, bool isNullable, ResolveResult expression)
		{
			IType t = context.GetClass(targetType) ?? SharedTypes.UnknownType;
			if (isNullable) t = NullableType.Create(t, context);
			return ResolveCast(t, expression);
		}
		
		static string GetOverloadableOperatorName(BinaryOperatorType op)
		{
			switch (op) {
				case BinaryOperatorType.Add:
					return "op_Addition";
				case BinaryOperatorType.Subtract:
					return "op_Subtraction";
				case BinaryOperatorType.Multiply:
					return "op_Multiply";
				case BinaryOperatorType.Divide:
					return "op_Division";
				case BinaryOperatorType.Modulus:
					return "op_Modulus";
				case BinaryOperatorType.BitwiseAnd:
					return "op_BitwiseAnd";
				case BinaryOperatorType.BitwiseOr:
					return "op_BitwiseOr";
				case BinaryOperatorType.ExclusiveOr:
					return "op_ExclusiveOr";
				case BinaryOperatorType.ShiftLeft:
					return "op_LeftShift";
				case BinaryOperatorType.ShiftRight:
					return "op_RightShift";
				case BinaryOperatorType.Equality:
					return "op_Equality";
				case BinaryOperatorType.InEquality:
					return "op_Inequality";
				case BinaryOperatorType.GreaterThan:
					return "op_GreaterThan";
				case BinaryOperatorType.LessThan:
					return "op_LessThan";
				case BinaryOperatorType.GreaterThanOrEqual:
					return "op_GreaterThanOrEqual";
				case BinaryOperatorType.LessThanOrEqual:
					return "op_LessThanOrEqual";
				default:
					return null;
			}
		}
		
		abstract class BinaryOperatorMethod : OperatorMethod
		{
			public virtual bool CanEvaluateAtCompileTime { get { return true; } }
			public abstract object Invoke(CSharpResolver resolver, object lhs, object rhs);
		}
		
		sealed class LambdaBinaryOperatorMethod<T> : BinaryOperatorMethod
		{
			readonly Func<T, T, T> func;
			
			public LambdaBinaryOperatorMethod(Func<T, T, T> func)
			{
				this.ReturnType = typeof(T).ToTypeReference();
				this.Parameters.Add(new DefaultParameter(this.ReturnType, string.Empty));
				this.Parameters.Add(new DefaultParameter(this.ReturnType, string.Empty));
				this.func = func;
			}
			
			public override object Invoke(CSharpResolver resolver, object lhs, object rhs)
			{
				TypeCode typeCode = Type.GetTypeCode(typeof(T));
				return func((T)resolver.CSharpPrimitiveCast(typeCode, lhs),
				            (T)resolver.CSharpPrimitiveCast(typeCode, rhs));
			}
			
			public override OperatorMethod Lift()
			{
				return new LiftedBinaryOperatorMethod(this);
			}
		}
		
		sealed class LiftedBinaryOperatorMethod : BinaryOperatorMethod, OverloadResolution.ILiftedOperator
		{
			readonly BinaryOperatorMethod baseMethod;
			
			public LiftedBinaryOperatorMethod(BinaryOperatorMethod baseMethod)
			{
				this.baseMethod = baseMethod;
				this.ReturnType = NullableType.Create(baseMethod.ReturnType);
				this.Parameters.Add(new DefaultParameter(NullableType.Create(baseMethod.Parameters[0].Type), string.Empty));
				this.Parameters.Add(new DefaultParameter(NullableType.Create(baseMethod.Parameters[1].Type), string.Empty));
			}
			
			public override object Invoke(CSharpResolver resolver, object lhs, object rhs)
			{
				if (lhs == null || rhs == null)
					return null;
				else
					return baseMethod.Invoke(resolver, lhs, rhs);
			}
			
			public IList<IParameter> NonLiftedParameters {
				get { return baseMethod.Parameters; }
			}
		}
		
		// C# 4.0 spec: §7.8.1 Multiplication operator
		static readonly OperatorMethod[] checkedMultiplicationOperators = Lift(
			new LambdaBinaryOperatorMethod<int>    ((a, b) => checked(a * b)),
			new LambdaBinaryOperatorMethod<uint>   ((a, b) => checked(a * b)),
			new LambdaBinaryOperatorMethod<long>   ((a, b) => checked(a * b)),
			new LambdaBinaryOperatorMethod<ulong>  ((a, b) => checked(a * b)),
			new LambdaBinaryOperatorMethod<float>  ((a, b) => checked(a * b)),
			new LambdaBinaryOperatorMethod<double> ((a, b) => checked(a * b)),
			new LambdaBinaryOperatorMethod<decimal>((a, b) => checked(a * b))
		);
		static readonly OperatorMethod[] uncheckedMultiplicationOperators = Lift(
			new LambdaBinaryOperatorMethod<int>    ((a, b) => unchecked(a * b)),
			new LambdaBinaryOperatorMethod<uint>   ((a, b) => unchecked(a * b)),
			new LambdaBinaryOperatorMethod<long>   ((a, b) => unchecked(a * b)),
			new LambdaBinaryOperatorMethod<ulong>  ((a, b) => unchecked(a * b)),
			new LambdaBinaryOperatorMethod<float>  ((a, b) => unchecked(a * b)),
			new LambdaBinaryOperatorMethod<double> ((a, b) => unchecked(a * b)),
			new LambdaBinaryOperatorMethod<decimal>((a, b) => unchecked(a * b))
		);
		
		// C# 4.0 spec: §7.8.2 Division operator
		static readonly OperatorMethod[] checkedDivisionOperators = Lift(
			new LambdaBinaryOperatorMethod<int>    ((a, b) => checked(a / b)),
			new LambdaBinaryOperatorMethod<uint>   ((a, b) => checked(a / b)),
			new LambdaBinaryOperatorMethod<long>   ((a, b) => checked(a / b)),
			new LambdaBinaryOperatorMethod<ulong>  ((a, b) => checked(a / b)),
			new LambdaBinaryOperatorMethod<float>  ((a, b) => checked(a / b)),
			new LambdaBinaryOperatorMethod<double> ((a, b) => checked(a / b)),
			new LambdaBinaryOperatorMethod<decimal>((a, b) => checked(a / b))
		);
		static readonly OperatorMethod[] uncheckedDivisionOperators = Lift(
			new LambdaBinaryOperatorMethod<int>    ((a, b) => unchecked(a / b)),
			new LambdaBinaryOperatorMethod<uint>   ((a, b) => unchecked(a / b)),
			new LambdaBinaryOperatorMethod<long>   ((a, b) => unchecked(a / b)),
			new LambdaBinaryOperatorMethod<ulong>  ((a, b) => unchecked(a / b)),
			new LambdaBinaryOperatorMethod<float>  ((a, b) => unchecked(a / b)),
			new LambdaBinaryOperatorMethod<double> ((a, b) => unchecked(a / b)),
			new LambdaBinaryOperatorMethod<decimal>((a, b) => unchecked(a / b))
		);
		
		// C# 4.0 spec: §7.8.3 Remainder operator
		static readonly OperatorMethod[] checkedRemainderOperators = Lift(
			new LambdaBinaryOperatorMethod<int>    ((a, b) => checked(a % b)),
			new LambdaBinaryOperatorMethod<uint>   ((a, b) => checked(a % b)),
			new LambdaBinaryOperatorMethod<long>   ((a, b) => checked(a % b)),
			new LambdaBinaryOperatorMethod<ulong>  ((a, b) => checked(a % b)),
			new LambdaBinaryOperatorMethod<float>  ((a, b) => checked(a % b)),
			new LambdaBinaryOperatorMethod<double> ((a, b) => checked(a % b)),
			new LambdaBinaryOperatorMethod<decimal>((a, b) => checked(a % b))
		);
		static readonly OperatorMethod[] uncheckedRemainderOperators = Lift(
			new LambdaBinaryOperatorMethod<int>    ((a, b) => unchecked(a % b)),
			new LambdaBinaryOperatorMethod<uint>   ((a, b) => unchecked(a % b)),
			new LambdaBinaryOperatorMethod<long>   ((a, b) => unchecked(a % b)),
			new LambdaBinaryOperatorMethod<ulong>  ((a, b) => unchecked(a % b)),
			new LambdaBinaryOperatorMethod<float>  ((a, b) => unchecked(a % b)),
			new LambdaBinaryOperatorMethod<double> ((a, b) => unchecked(a % b)),
			new LambdaBinaryOperatorMethod<decimal>((a, b) => unchecked(a % b))
		);
		
		// C# 4.0 spec: §7.8.3 Addition operator
		static readonly OperatorMethod[] checkedAdditionOperators = Lift(
			new LambdaBinaryOperatorMethod<int>    ((a, b) => checked(a + b)),
			new LambdaBinaryOperatorMethod<uint>   ((a, b) => checked(a + b)),
			new LambdaBinaryOperatorMethod<long>   ((a, b) => checked(a + b)),
			new LambdaBinaryOperatorMethod<ulong>  ((a, b) => checked(a + b)),
			new LambdaBinaryOperatorMethod<float>  ((a, b) => checked(a + b)),
			new LambdaBinaryOperatorMethod<double> ((a, b) => checked(a + b)),
			new LambdaBinaryOperatorMethod<decimal>((a, b) => checked(a + b)),
			new StringConcatenation(typeof(string), typeof(string)),
			new StringConcatenation(typeof(string), typeof(object)),
			new StringConcatenation(typeof(object), typeof(string))
		);
		static readonly OperatorMethod[] uncheckedAdditionOperators = Lift(
			new LambdaBinaryOperatorMethod<int>    ((a, b) => unchecked(a + b)),
			new LambdaBinaryOperatorMethod<uint>   ((a, b) => unchecked(a + b)),
			new LambdaBinaryOperatorMethod<long>   ((a, b) => unchecked(a + b)),
			new LambdaBinaryOperatorMethod<ulong>  ((a, b) => unchecked(a + b)),
			new LambdaBinaryOperatorMethod<float>  ((a, b) => unchecked(a + b)),
			new LambdaBinaryOperatorMethod<double> ((a, b) => unchecked(a + b)),
			new LambdaBinaryOperatorMethod<decimal>((a, b) => unchecked(a + b)),
			new StringConcatenation(typeof(string), typeof(string)),
			new StringConcatenation(typeof(string), typeof(object)),
			new StringConcatenation(typeof(object), typeof(string))
		);
		// not in this list, but handled manually: enum addition, delegate combination
		sealed class StringConcatenation : BinaryOperatorMethod
		{
			bool canEvaluateAtCompileTime;
			
			public StringConcatenation(Type p1, Type p2)
			{
				this.canEvaluateAtCompileTime = p1 == typeof(string) && p2 == typeof(string);
				this.ReturnType = typeof(string).ToTypeReference();
				this.Parameters.Add(new DefaultParameter(p1.ToTypeReference(), string.Empty));
				this.Parameters.Add(new DefaultParameter(p2.ToTypeReference(), string.Empty));
			}
			
			public override bool CanEvaluateAtCompileTime {
				get { return canEvaluateAtCompileTime; }
			}
			
			public override object Invoke(CSharpResolver resolver, object lhs, object rhs)
			{
				return string.Concat(lhs, rhs);
			}
		}
		
		object GetUserBinaryOperatorCandidates()
		{
			// C# 4.0 spec: §7.3.5 Candidate user-defined operators
			// TODO: implement user-defined operators
			throw new NotImplementedException();
		}
		#endregion
		
		#region ResolveCast
		public ResolveResult ResolveCast(IType targetType, ResolveResult expression)
		{
			// C# 4.0 spec: §7.7.6 Cast expressions
			if (expression.IsCompileTimeConstant) {
				TypeCode code = ReflectionHelper.GetTypeCode(targetType);
				if (code >= TypeCode.Boolean && code <= TypeCode.Decimal && expression.ConstantValue != null) {
					try {
						return new ConstantResolveResult(targetType, CSharpPrimitiveCast(code, expression.ConstantValue));
					} catch (OverflowException) {
						return new ErrorResolveResult(targetType);
					}
				} else if (code == TypeCode.String) {
					if (expression.ConstantValue == null || expression.ConstantValue is string)
						return new ConstantResolveResult(targetType, expression.ConstantValue);
					else
						return new ErrorResolveResult(targetType);
				} else if (targetType.IsEnum()) {
					code = ReflectionHelper.GetTypeCode(targetType.GetEnumUnderlyingType(context));
					if (code >= TypeCode.SByte && code <= TypeCode.UInt64 && expression.ConstantValue != null) {
						try {
							return new ConstantResolveResult(targetType, CSharpPrimitiveCast(code, expression.ConstantValue));
						} catch (OverflowException) {
							return new ErrorResolveResult(targetType);
						}
					}
				}
			}
			return new ResolveResult(targetType);
		}
		
		ResolveResult CheckErrorAndResolveCast(IType targetType, ResolveResult expression)
		{
			if (expression.IsError)
				return expression;
			else
				return ResolveCast(targetType, expression);
		}
		
		#region CSharpPrimitiveCast
		/// <summary>
		/// Performs a conversion between primitive types.
		/// Unfortunately we cannot use Convert.ChangeType because it has different semantics
		/// (e.g. rounding behavior for floats, overflow, etc.), so we write down every possible primitive C# cast
		/// and let the compiler figure out the exact semantics.
		/// And we have to do everything twice, once in a checked-block, once in an unchecked-block.
		/// </summary>
		object CSharpPrimitiveCast(TypeCode targetType, object input)
		{
			if (IsCheckedContext)
				return CSharpPrimitiveCastChecked(targetType, input);
			else
				return CSharpPrimitiveCastUnchecked(targetType, input);
		}
		
		static object CSharpPrimitiveCastChecked(TypeCode targetType, object input)
		{
			checked {
				TypeCode sourceType = Type.GetTypeCode(input.GetType());
				if (sourceType == targetType)
					return input;
				switch (targetType) {
					case TypeCode.Char:
						switch (sourceType) {
								case TypeCode.SByte:   return (char)(sbyte)input;
								case TypeCode.Byte:    return (char)(byte)input;
								case TypeCode.Int16:   return (char)(short)input;
								case TypeCode.UInt16:  return (char)(ushort)input;
								case TypeCode.Int32:   return (char)(int)input;
								case TypeCode.UInt32:  return (char)(uint)input;
								case TypeCode.Int64:   return (char)(long)input;
								case TypeCode.UInt64:  return (char)(ulong)input;
								case TypeCode.Single:  return (char)(float)input;
								case TypeCode.Double:  return (char)(double)input;
								case TypeCode.Decimal: return (char)(decimal)input;
						}
						break;
					case TypeCode.SByte:
						switch (sourceType) {
								case TypeCode.Char:    return (sbyte)(char)input;
								case TypeCode.Byte:    return (sbyte)(byte)input;
								case TypeCode.Int16:   return (sbyte)(short)input;
								case TypeCode.UInt16:  return (sbyte)(ushort)input;
								case TypeCode.Int32:   return (sbyte)(int)input;
								case TypeCode.UInt32:  return (sbyte)(uint)input;
								case TypeCode.Int64:   return (sbyte)(long)input;
								case TypeCode.UInt64:  return (sbyte)(ulong)input;
								case TypeCode.Single:  return (sbyte)(float)input;
								case TypeCode.Double:  return (sbyte)(double)input;
								case TypeCode.Decimal: return (sbyte)(decimal)input;
						}
						break;
					case TypeCode.Byte:
						switch (sourceType) {
								case TypeCode.Char:    return (byte)(char)input;
								case TypeCode.SByte:   return (byte)(sbyte)input;
								case TypeCode.Int16:   return (byte)(short)input;
								case TypeCode.UInt16:  return (byte)(ushort)input;
								case TypeCode.Int32:   return (byte)(int)input;
								case TypeCode.UInt32:  return (byte)(uint)input;
								case TypeCode.Int64:   return (byte)(long)input;
								case TypeCode.UInt64:  return (byte)(ulong)input;
								case TypeCode.Single:  return (byte)(float)input;
								case TypeCode.Double:  return (byte)(double)input;
								case TypeCode.Decimal: return (byte)(decimal)input;
						}
						break;
					case TypeCode.Int16:
						switch (sourceType) {
								case TypeCode.Char:    return (short)(char)input;
								case TypeCode.SByte:   return (short)(sbyte)input;
								case TypeCode.Byte:    return (short)(byte)input;
								case TypeCode.UInt16:  return (short)(ushort)input;
								case TypeCode.Int32:   return (short)(int)input;
								case TypeCode.UInt32:  return (short)(uint)input;
								case TypeCode.Int64:   return (short)(long)input;
								case TypeCode.UInt64:  return (short)(ulong)input;
								case TypeCode.Single:  return (short)(float)input;
								case TypeCode.Double:  return (short)(double)input;
								case TypeCode.Decimal: return (short)(decimal)input;
						}
						break;
					case TypeCode.UInt16:
						switch (sourceType) {
								case TypeCode.Char:    return (ushort)(char)input;
								case TypeCode.SByte:   return (ushort)(sbyte)input;
								case TypeCode.Byte:    return (ushort)(byte)input;
								case TypeCode.Int16:   return (ushort)(short)input;
								case TypeCode.Int32:   return (ushort)(int)input;
								case TypeCode.UInt32:  return (ushort)(uint)input;
								case TypeCode.Int64:   return (ushort)(long)input;
								case TypeCode.UInt64:  return (ushort)(ulong)input;
								case TypeCode.Single:  return (ushort)(float)input;
								case TypeCode.Double:  return (ushort)(double)input;
								case TypeCode.Decimal: return (ushort)(decimal)input;
						}
						break;
					case TypeCode.Int32:
						switch (sourceType) {
								case TypeCode.Char:    return (int)(char)input;
								case TypeCode.SByte:   return (int)(sbyte)input;
								case TypeCode.Byte:    return (int)(byte)input;
								case TypeCode.Int16:   return (int)(short)input;
								case TypeCode.UInt16:  return (int)(ushort)input;
								case TypeCode.UInt32:  return (int)(uint)input;
								case TypeCode.Int64:   return (int)(long)input;
								case TypeCode.UInt64:  return (int)(ulong)input;
								case TypeCode.Single:  return (int)(float)input;
								case TypeCode.Double:  return (int)(double)input;
								case TypeCode.Decimal: return (int)(decimal)input;
						}
						break;
					case TypeCode.UInt32:
						switch (sourceType) {
								case TypeCode.Char:    return (uint)(char)input;
								case TypeCode.SByte:   return (uint)(sbyte)input;
								case TypeCode.Byte:    return (uint)(byte)input;
								case TypeCode.Int16:   return (uint)(short)input;
								case TypeCode.UInt16:  return (uint)(ushort)input;
								case TypeCode.Int32:   return (uint)(int)input;
								case TypeCode.Int64:   return (uint)(long)input;
								case TypeCode.UInt64:  return (uint)(ulong)input;
								case TypeCode.Single:  return (uint)(float)input;
								case TypeCode.Double:  return (uint)(double)input;
								case TypeCode.Decimal: return (uint)(decimal)input;
						}
						break;
					case TypeCode.Int64:
						switch (sourceType) {
								case TypeCode.Char:    return (long)(char)input;
								case TypeCode.SByte:   return (long)(sbyte)input;
								case TypeCode.Byte:    return (long)(byte)input;
								case TypeCode.Int16:   return (long)(short)input;
								case TypeCode.UInt16:  return (long)(ushort)input;
								case TypeCode.Int32:   return (long)(int)input;
								case TypeCode.UInt32:  return (long)(uint)input;
								case TypeCode.UInt64:  return (long)(ulong)input;
								case TypeCode.Single:  return (long)(float)input;
								case TypeCode.Double:  return (long)(double)input;
								case TypeCode.Decimal: return (long)(decimal)input;
						}
						break;
					case TypeCode.UInt64:
						switch (sourceType) {
								case TypeCode.Char:    return (ulong)(char)input;
								case TypeCode.SByte:   return (ulong)(sbyte)input;
								case TypeCode.Byte:    return (ulong)(byte)input;
								case TypeCode.Int16:   return (ulong)(short)input;
								case TypeCode.UInt16:  return (ulong)(ushort)input;
								case TypeCode.Int32:   return (ulong)(int)input;
								case TypeCode.UInt32:  return (ulong)(uint)input;
								case TypeCode.Int64:   return (ulong)(long)input;
								case TypeCode.Single:  return (ulong)(float)input;
								case TypeCode.Double:  return (ulong)(double)input;
								case TypeCode.Decimal: return (ulong)(decimal)input;
						}
						break;
					case TypeCode.Single:
						switch (sourceType) {
								case TypeCode.Char:    return (float)(char)input;
								case TypeCode.SByte:   return (float)(sbyte)input;
								case TypeCode.Byte:    return (float)(byte)input;
								case TypeCode.Int16:   return (float)(short)input;
								case TypeCode.UInt16:  return (float)(ushort)input;
								case TypeCode.Int32:   return (float)(int)input;
								case TypeCode.UInt32:  return (float)(uint)input;
								case TypeCode.Int64:   return (float)(long)input;
								case TypeCode.UInt64:  return (float)(ulong)input;
								case TypeCode.Double:  return (float)(double)input;
								case TypeCode.Decimal: return (float)(decimal)input;
						}
						break;
					case TypeCode.Double:
						switch (sourceType) {
								case TypeCode.Char:    return (double)(char)input;
								case TypeCode.SByte:   return (double)(sbyte)input;
								case TypeCode.Byte:    return (double)(byte)input;
								case TypeCode.Int16:   return (double)(short)input;
								case TypeCode.UInt16:  return (double)(ushort)input;
								case TypeCode.Int32:   return (double)(int)input;
								case TypeCode.UInt32:  return (double)(uint)input;
								case TypeCode.Int64:   return (double)(long)input;
								case TypeCode.UInt64:  return (double)(ulong)input;
								case TypeCode.Single:  return (double)(float)input;
								case TypeCode.Decimal: return (double)(decimal)input;
						}
						break;
					case TypeCode.Decimal:
						switch (sourceType) {
								case TypeCode.Char:    return (decimal)(char)input;
								case TypeCode.SByte:   return (decimal)(sbyte)input;
								case TypeCode.Byte:    return (decimal)(byte)input;
								case TypeCode.Int16:   return (decimal)(short)input;
								case TypeCode.UInt16:  return (decimal)(ushort)input;
								case TypeCode.Int32:   return (decimal)(int)input;
								case TypeCode.UInt32:  return (decimal)(uint)input;
								case TypeCode.Int64:   return (decimal)(long)input;
								case TypeCode.UInt64:  return (decimal)(ulong)input;
								case TypeCode.Single:  return (decimal)(float)input;
								case TypeCode.Double:  return (decimal)(double)input;
						}
						break;
				}
				throw new InvalidCastException("Cast from " + sourceType + " to " + targetType + "not supported.");
			}
		}
		
		static object CSharpPrimitiveCastUnchecked(TypeCode targetType, object input)
		{
			unchecked {
				TypeCode sourceType = Type.GetTypeCode(input.GetType());
				if (sourceType == targetType)
					return input;
				switch (targetType) {
					case TypeCode.Char:
						switch (sourceType) {
								case TypeCode.SByte:   return (char)(sbyte)input;
								case TypeCode.Byte:    return (char)(byte)input;
								case TypeCode.Int16:   return (char)(short)input;
								case TypeCode.UInt16:  return (char)(ushort)input;
								case TypeCode.Int32:   return (char)(int)input;
								case TypeCode.UInt32:  return (char)(uint)input;
								case TypeCode.Int64:   return (char)(long)input;
								case TypeCode.UInt64:  return (char)(ulong)input;
								case TypeCode.Single:  return (char)(float)input;
								case TypeCode.Double:  return (char)(double)input;
								case TypeCode.Decimal: return (char)(decimal)input;
						}
						break;
					case TypeCode.SByte:
						switch (sourceType) {
								case TypeCode.Char:    return (sbyte)(char)input;
								case TypeCode.Byte:    return (sbyte)(byte)input;
								case TypeCode.Int16:   return (sbyte)(short)input;
								case TypeCode.UInt16:  return (sbyte)(ushort)input;
								case TypeCode.Int32:   return (sbyte)(int)input;
								case TypeCode.UInt32:  return (sbyte)(uint)input;
								case TypeCode.Int64:   return (sbyte)(long)input;
								case TypeCode.UInt64:  return (sbyte)(ulong)input;
								case TypeCode.Single:  return (sbyte)(float)input;
								case TypeCode.Double:  return (sbyte)(double)input;
								case TypeCode.Decimal: return (sbyte)(decimal)input;
						}
						break;
					case TypeCode.Byte:
						switch (sourceType) {
								case TypeCode.Char:    return (byte)(char)input;
								case TypeCode.SByte:   return (byte)(sbyte)input;
								case TypeCode.Int16:   return (byte)(short)input;
								case TypeCode.UInt16:  return (byte)(ushort)input;
								case TypeCode.Int32:   return (byte)(int)input;
								case TypeCode.UInt32:  return (byte)(uint)input;
								case TypeCode.Int64:   return (byte)(long)input;
								case TypeCode.UInt64:  return (byte)(ulong)input;
								case TypeCode.Single:  return (byte)(float)input;
								case TypeCode.Double:  return (byte)(double)input;
								case TypeCode.Decimal: return (byte)(decimal)input;
						}
						break;
					case TypeCode.Int16:
						switch (sourceType) {
								case TypeCode.Char:    return (short)(char)input;
								case TypeCode.SByte:   return (short)(sbyte)input;
								case TypeCode.Byte:    return (short)(byte)input;
								case TypeCode.UInt16:  return (short)(ushort)input;
								case TypeCode.Int32:   return (short)(int)input;
								case TypeCode.UInt32:  return (short)(uint)input;
								case TypeCode.Int64:   return (short)(long)input;
								case TypeCode.UInt64:  return (short)(ulong)input;
								case TypeCode.Single:  return (short)(float)input;
								case TypeCode.Double:  return (short)(double)input;
								case TypeCode.Decimal: return (short)(decimal)input;
						}
						break;
					case TypeCode.UInt16:
						switch (sourceType) {
								case TypeCode.Char:    return (ushort)(char)input;
								case TypeCode.SByte:   return (ushort)(sbyte)input;
								case TypeCode.Byte:    return (ushort)(byte)input;
								case TypeCode.Int16:   return (ushort)(short)input;
								case TypeCode.Int32:   return (ushort)(int)input;
								case TypeCode.UInt32:  return (ushort)(uint)input;
								case TypeCode.Int64:   return (ushort)(long)input;
								case TypeCode.UInt64:  return (ushort)(ulong)input;
								case TypeCode.Single:  return (ushort)(float)input;
								case TypeCode.Double:  return (ushort)(double)input;
								case TypeCode.Decimal: return (ushort)(decimal)input;
						}
						break;
					case TypeCode.Int32:
						switch (sourceType) {
								case TypeCode.Char:    return (int)(char)input;
								case TypeCode.SByte:   return (int)(sbyte)input;
								case TypeCode.Byte:    return (int)(byte)input;
								case TypeCode.Int16:   return (int)(short)input;
								case TypeCode.UInt16:  return (int)(ushort)input;
								case TypeCode.UInt32:  return (int)(uint)input;
								case TypeCode.Int64:   return (int)(long)input;
								case TypeCode.UInt64:  return (int)(ulong)input;
								case TypeCode.Single:  return (int)(float)input;
								case TypeCode.Double:  return (int)(double)input;
								case TypeCode.Decimal: return (int)(decimal)input;
						}
						break;
					case TypeCode.UInt32:
						switch (sourceType) {
								case TypeCode.Char:    return (uint)(char)input;
								case TypeCode.SByte:   return (uint)(sbyte)input;
								case TypeCode.Byte:    return (uint)(byte)input;
								case TypeCode.Int16:   return (uint)(short)input;
								case TypeCode.UInt16:  return (uint)(ushort)input;
								case TypeCode.Int32:   return (uint)(int)input;
								case TypeCode.Int64:   return (uint)(long)input;
								case TypeCode.UInt64:  return (uint)(ulong)input;
								case TypeCode.Single:  return (uint)(float)input;
								case TypeCode.Double:  return (uint)(double)input;
								case TypeCode.Decimal: return (uint)(decimal)input;
						}
						break;
					case TypeCode.Int64:
						switch (sourceType) {
								case TypeCode.Char:    return (long)(char)input;
								case TypeCode.SByte:   return (long)(sbyte)input;
								case TypeCode.Byte:    return (long)(byte)input;
								case TypeCode.Int16:   return (long)(short)input;
								case TypeCode.UInt16:  return (long)(ushort)input;
								case TypeCode.Int32:   return (long)(int)input;
								case TypeCode.UInt32:  return (long)(uint)input;
								case TypeCode.UInt64:  return (long)(ulong)input;
								case TypeCode.Single:  return (long)(float)input;
								case TypeCode.Double:  return (long)(double)input;
								case TypeCode.Decimal: return (long)(decimal)input;
						}
						break;
					case TypeCode.UInt64:
						switch (sourceType) {
								case TypeCode.Char:    return (ulong)(char)input;
								case TypeCode.SByte:   return (ulong)(sbyte)input;
								case TypeCode.Byte:    return (ulong)(byte)input;
								case TypeCode.Int16:   return (ulong)(short)input;
								case TypeCode.UInt16:  return (ulong)(ushort)input;
								case TypeCode.Int32:   return (ulong)(int)input;
								case TypeCode.UInt32:  return (ulong)(uint)input;
								case TypeCode.Int64:   return (ulong)(long)input;
								case TypeCode.Single:  return (ulong)(float)input;
								case TypeCode.Double:  return (ulong)(double)input;
								case TypeCode.Decimal: return (ulong)(decimal)input;
						}
						break;
					case TypeCode.Single:
						switch (sourceType) {
								case TypeCode.Char:    return (float)(char)input;
								case TypeCode.SByte:   return (float)(sbyte)input;
								case TypeCode.Byte:    return (float)(byte)input;
								case TypeCode.Int16:   return (float)(short)input;
								case TypeCode.UInt16:  return (float)(ushort)input;
								case TypeCode.Int32:   return (float)(int)input;
								case TypeCode.UInt32:  return (float)(uint)input;
								case TypeCode.Int64:   return (float)(long)input;
								case TypeCode.UInt64:  return (float)(ulong)input;
								case TypeCode.Double:  return (float)(double)input;
								case TypeCode.Decimal: return (float)(decimal)input;
						}
						break;
					case TypeCode.Double:
						switch (sourceType) {
								case TypeCode.Char:    return (double)(char)input;
								case TypeCode.SByte:   return (double)(sbyte)input;
								case TypeCode.Byte:    return (double)(byte)input;
								case TypeCode.Int16:   return (double)(short)input;
								case TypeCode.UInt16:  return (double)(ushort)input;
								case TypeCode.Int32:   return (double)(int)input;
								case TypeCode.UInt32:  return (double)(uint)input;
								case TypeCode.Int64:   return (double)(long)input;
								case TypeCode.UInt64:  return (double)(ulong)input;
								case TypeCode.Single:  return (double)(float)input;
								case TypeCode.Decimal: return (double)(decimal)input;
						}
						break;
					case TypeCode.Decimal:
						switch (sourceType) {
								case TypeCode.Char:    return (decimal)(char)input;
								case TypeCode.SByte:   return (decimal)(sbyte)input;
								case TypeCode.Byte:    return (decimal)(byte)input;
								case TypeCode.Int16:   return (decimal)(short)input;
								case TypeCode.UInt16:  return (decimal)(ushort)input;
								case TypeCode.Int32:   return (decimal)(int)input;
								case TypeCode.UInt32:  return (decimal)(uint)input;
								case TypeCode.Int64:   return (decimal)(long)input;
								case TypeCode.UInt64:  return (decimal)(ulong)input;
								case TypeCode.Single:  return (decimal)(float)input;
								case TypeCode.Double:  return (decimal)(double)input;
						}
						break;
				}
				throw new InvalidCastException("Cast from " + sourceType + " to " + targetType + "not supported.");
			}
		}
		#endregion
		#endregion
	}
}
