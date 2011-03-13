using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Helpers
{
	/// <summary>
	/// Contains various utility functions for working with collections.
	/// </summary>
	public static class C
	{
		public static TOut[] Map<TIn, TOut>(ICollection<TIn> collection, Converter<TIn, TOut> converter)
		{
			return Array.ConvertAll(ToArray<TIn>(collection), converter);
		}

		public delegate T Reducer<T>(T t1, T t2);
		public static T Reduce<T>(ICollection<T> collection, Reducer<T> reducer)
		{
			T[] array = ToArray<T>(collection);

			if (array.Length == 0)
			{
				return default(T);
			}

			T result = array[0];

			for (int i = 1; i < array.Length; i++)
			{
				result = reducer(result, array[i]);
			}

			return result;
		}

		public static T[] Filter<T>(ICollection<T> collection, Predicate<T> predicate)
		{
			List<T> result = new List<T>();
			foreach (T t in collection)
			{
				if (predicate(t))
				{
					result.Add(t);
				}
			}

			return result.ToArray();
		}

		public static T[] Uniq<T>(ICollection<T> collection)
		{
			List<T> list = new List<T>(collection.Count);
			foreach (T t in collection)
			{
				if (!list.Contains(t))
				{
					list.Add(t);
				}
			}

			return list.ToArray();
		}

		public static T[] Concat<T>(params ICollection<T>[] collections)
		{
			T[] result = new T[Sum(Map<ICollection<T>, int>(collections, delegate(ICollection<T> c) { return c.Count; }))];
			int i = 0;
			foreach (ICollection<T> collection in collections)
			{
				Array.Copy(ToArray<T>(collection), 0, result, i, collection.Count);
				i += collection.Count;
			}
			return result;
		}

		public static bool Eq(ICollection lhs, ICollection rhs)
		{
			if (lhs.Count != rhs.Count)
			{
				return false;
			}

			IEnumerator lhsEnumerator = lhs.GetEnumerator();
			IEnumerator rhsEnumerator = rhs.GetEnumerator();

			while (lhsEnumerator.MoveNext() && rhsEnumerator.MoveNext())
			{
				if (lhsEnumerator.Current is ICollection && rhsEnumerator.Current is ICollection)
				{
					if (!Eq((ICollection)lhsEnumerator.Current, (ICollection)rhsEnumerator.Current))
					{
						return false;
					}
				}
				else
				{
					if (!lhsEnumerator.Current.Equals(rhsEnumerator.Current))
					{
						return false;
					}
				}
			}

			return true;
		}

		public static T[] ToArray<T>(IEnumerable enumerable)
		{
			// try to avoid copying if possible
			if (enumerable is T[])
			{
				return (T[])enumerable;
			}
			else if (enumerable is ArrayList)
			{
				return (T[])((ArrayList)enumerable).ToArray(typeof(T));
			}
			else if (enumerable is List<T>)
			{
				return ((List<T>)enumerable).ToArray();
			}

			// fallback - works for any IEnumerable
			List<T> list = new List<T>();
			foreach (T element in enumerable)
			{
				list.Add(element);
			}
			return list.ToArray();
		}

		#region Min, Max

		public static T Min<T>(ICollection<T> collection) where T : IComparable
		{
			bool iterationStarted = false;
			T min = default(T);

			foreach (T t in collection)
			{
				if (!iterationStarted)
				{
					min = t;
					iterationStarted = true;
				}
				else
				{
					if (t.CompareTo(min) == -1)
					{
						min = t;
					}
				}
			}

			return min;
		}

		public static T Min<T>(params T[] elements) where T : IComparable
		{
			return Min((ICollection<T>)elements);
		}

		public static T Max<T>(ICollection<T> collection) where T : IComparable
		{
			bool iterationStarted = false;
			T max = default(T);

			foreach (T t in collection)
			{
				if (!iterationStarted)
				{
					max = t;
					iterationStarted = true;
				}
				else
				{
					if (t.CompareTo(max) == 1)
					{
						max = t;
					}
				}
			}

			return max;
		}

		public static T Max<T>(params T[] elements) where T : IComparable
		{
			return Max((ICollection<T>)elements);
		}

		#endregion

		#region Sum, Product

		public static T Sum<T>(ICollection<T> collection)
		{
			return Reduce(collection, new Reducer<T>(M.GetOperator<T>(M.Operator.Add)));
		}

		public static T Sum<T>(params T[] terms)
		{
			return Sum((ICollection<T>)terms);
		}

		public static T Product<T>(ICollection<T> collection)
		{
			return Reduce<T>(collection, new Reducer<T>(M.GetOperator<T>(M.Operator.Mul)));
		}

		public static T Product<T>(params T[] factors)
		{
			return Product((ICollection<T>)factors);
		}

		#endregion
	}
}
