using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;

namespace Helpers
{
	/// <summary>
	/// Contains various utility functions for working with types.
	/// </summary>
	public class M
	{
		public delegate T Func2<T>(T lhs, T rhs);

		public enum Operator
		{
			[OperatorCallInfo(MethodName = "Addition", OpCodeName = "Add")]
			Add,
			[OperatorCallInfo(MethodName = "Subtraction", OpCodeName = "Sub")]
			Sub,
			[OperatorCallInfo(MethodName = "Multiply", OpCodeName = "Mul")]
			Mul,
			[OperatorCallInfo(MethodName = "Division", OpCodeName = "Div", UnsignedOpCodeName = "Div_Un")]
			Div,
			[OperatorCallInfo(MethodName = "BitwiseAnd", OpCodeName = "And")]
			And,
			[OperatorCallInfo(MethodName = "BitwiseOr", OpCodeName = "Or")]
			Or,
			[OperatorCallInfo(MethodName = "ExclusiveOr", OpCodeName = "Xor")]
			Xor,
		}

		[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
		internal class OperatorCallInfoAttribute : Attribute
		{
			private string _MethodName;
			public string MethodName
			{
				get { return _MethodName; }
				set { _MethodName = value; }
			}

			private string _opCodeName;
			public string OpCodeName
			{
				get { return _opCodeName; }
				set { _opCodeName = value; }
			}

			private string _unsignedOpCodeName;
			public string UnsignedOpCodeName
			{
				get { return _unsignedOpCodeName; }
				set { _unsignedOpCodeName = value; }
			}
		}

		private static readonly Type[] UnsignedTypes = { typeof(byte), typeof(ushort), typeof(uint), typeof(ulong) };

		private static readonly Dictionary<Type, Dictionary<Operator, Delegate>> _delegates = new Dictionary<Type, Dictionary<Operator, Delegate>>();
		private static readonly ReaderWriterLock _delegatesLock = new ReaderWriterLock();

		private static Func2<T> GetOperatorMethod<T>(Operator op)
		{
			string name = ((OperatorCallInfoAttribute)Attribute.GetCustomAttribute(typeof(Operator).GetField(op.ToString()), typeof(OperatorCallInfoAttribute))).MethodName;
			MethodInfo info = typeof(T).GetMethod("op_" + name, BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(T), typeof(T) }, null);
			return info == null ? null : (Func2<T>)Delegate.CreateDelegate(typeof(Func2<T>), info);
		}

		private static Func2<T> GetOperatorOpCode<T>(Operator op)
		{
			OperatorCallInfoAttribute attribute = (OperatorCallInfoAttribute)Attribute.GetCustomAttribute(typeof(Operator).GetField(op.ToString()), typeof(OperatorCallInfoAttribute));
			string name = attribute.UnsignedOpCodeName != null && Array.IndexOf(UnsignedTypes, typeof(T)) != -1 ? attribute.UnsignedOpCodeName : attribute.OpCodeName;
			OpCode opCode = (OpCode)typeof(OpCodes).GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);

			DynamicMethod method = new DynamicMethod("", typeof(T), new Type[] { typeof(T), typeof(T) }, typeof(int).Module);
			ILGenerator generator = method.GetILGenerator();
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldarg_1);
			generator.Emit(opCode);
			generator.Emit(OpCodes.Ret);
			return (Func2<T>)method.CreateDelegate(typeof(Func2<T>));
		}

		private static Func2<T> GetOperatorDirect<T>(Operator op)
		{
			return typeof(T).IsPrimitive ? GetOperatorOpCode<T>(op) : GetOperatorMethod<T>(op);
		}

		public static Func2<T> GetOperator<T>(Operator op)
		{
			Type type = typeof(T);
			_delegatesLock.AcquireReaderLock(Timeout.Infinite);
			try
			{
				if (!(_delegates.ContainsKey(type) && _delegates[type].ContainsKey(op)))
				{
					LockCookie cookie = _delegatesLock.UpgradeToWriterLock(Timeout.Infinite);
					try
					{
						if (!_delegates.ContainsKey(type))
						{
							_delegates[type] = new Dictionary<Operator, Delegate>();
						}
						_delegates[type][op] = GetOperatorDirect<T>(op);
					}
					finally
					{
						_delegatesLock.DowngradeFromWriterLock(ref cookie);
					}
				}

				return (Func2<T>)_delegates[type][op];
			}
			finally
			{
				_delegatesLock.ReleaseReaderLock();
			}
		}
	}
}
