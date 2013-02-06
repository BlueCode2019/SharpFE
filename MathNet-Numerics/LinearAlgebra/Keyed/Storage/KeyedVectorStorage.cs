﻿// <copyright file="KeyedVectorStorage.cs" company="Iain Sproat">
// Copyright Iain Sproat, 2013.
//
// Parts of this file are copyright and licensed under the following terms:
//
// Math.NET Numerics, part of the Math.NET Project
// http://numerics.mathdotnet.com
// http://github.com/mathnet/mathnet-numerics
// http://mathnetnumerics.codeplex.com
// Copyright (c) 2009-2010 Math.NET
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
// </copyright>

using System;
using MathNet.Numerics.Properties;

namespace MathNet.Numerics.LinearAlgebra.Keyed.Storage
{
    [Serializable]
    public abstract partial class KeyedVectorStorage<T> : IEquatable<KeyedVectorStorage<T>>
        where T : struct, IEquatable<T>, IFormattable
    {
        // [ruegg] public fields are OK here

        public readonly int Length;

        protected KeyedVectorStorage(int length)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(Resources.ArgumentMustBePositive);
            }

            Length = length;
        }

        /// <summary>
        /// Gets or sets the value at the given index, with range checking.
        /// </summary>
        /// <param name="index">
        /// The index of the element.
        /// </param>
        /// <value>The value to get or set.</value>
        /// <remarks>This method is ranged checked. <see cref="At(int)"/> and <see cref="At(int,T)"/>
        /// to get and set values without range checking.</remarks>
        public T this[int index]
        {
            get
            {
                ValidateRange(index);
                return At(index);
            }

            set
            {
                ValidateRange(index);
                At(index, value);
            }
        }

        /// <summary>
        /// Retrieves the requested element without range checking.
        /// </summary>
        /// <param name="index">The index of the element.</param>
        /// <returns>The requested element.</returns>
        /// <remarks>Not range-checked.</remarks>
        public abstract T At(int index);

        /// <summary>
        /// Sets the element without range checking.
        /// </summary>
        /// <param name="index">The index of the element.</param>
        /// <param name="value">The value to set the element to. </param>
        /// <remarks>WARNING: This method is not thread safe. Use "lock" with it and be sure to avoid deadlocks.</remarks>
        public abstract void At(int index, T value);

        /// <summary>
        /// True if all fields of this vector can be set to any value.
        /// False if some fields are fixed.
        /// </summary>
        public virtual bool IsFullyMutable
        {
            get { return true; }
        }

        /// <summary>
        /// True if the specified field can be set to any value.
        /// False if the field is fixed.
        /// </summary>
        public virtual bool IsMutable(int index)
        {
            return true;
        }

        public virtual void Clear()
        {
            for (var i = 0; i < Length; i++)
            {
                At(i, default(T));
            }
        }

        public virtual void Clear(int index, int count)
        {
            for (var i = index; i < index + count; i++)
            {
                At(i, default(T));
            }
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">
        /// An object to compare with this object.
        /// </param>
        /// <returns>
        /// <c>true</c> if the current object is equal to the <paramref name="other"/> parameter; otherwise, <c>false</c>.
        /// </returns>
        public virtual bool Equals(KeyedVectorStorage<T> other)
        {
            // Reject equality when the argument is null or has a different shape.
            if (other == null)
            {
                return false;
            }
            if (Length != other.Length)
            {
                return false;
            }

            // Accept if the argument is the same object as this.
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            // If all else fails, perform element wise comparison.
            for (var index = 0; index < Length; index++)
            {
                if (!At(index).Equals(other.At(index)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>. </param>
        public override sealed bool Equals(object obj)
        {
            return Equals(obj as KeyedMatrixStorage<T>);
        }

        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            var hashNum = Math.Min(Length, 25);
            int hash = 17;
            unchecked
            {
                for (var i = 0; i < hashNum; i++)
                {
                    hash = hash*31 + At(i).GetHashCode();
                }
            }
            return hash;
        }

        public void CopyTo(KeyedVectorStorage<T> target, bool skipClearing = false)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            if (ReferenceEquals(this, target))
            {
                return;
            }

            if (Length != target.Length)
            {
                throw new ArgumentException(Resources.ArgumentVectorsSameLength, "target");
            }

            CopyToUnchecked(target, skipClearing);
        }

        internal virtual void CopyToUnchecked(KeyedVectorStorage<T> target, bool skipClearing = false)
        {
            for (int i = 0; i < Length; i++)
            {
                target.At(i, At(i));
            }
        }

        public void CopySubVectorTo(KeyedVectorStorage<T> target,
            int sourceIndex, int targetIndex, int count,
            bool skipClearing = false)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            ValidateSubVectorRange(target, sourceIndex, targetIndex, count);

            CopySubVectorToUnchecked(target, sourceIndex, targetIndex, count, skipClearing);
        }

        internal virtual void CopySubVectorToUnchecked(KeyedVectorStorage<T> target,
            int sourceIndex, int targetIndex, int count,
            bool skipClearing = false)
        {
            if (ReferenceEquals(this, target))
            {
                var tmp = new T[count];
                for (int i = 0; i < tmp.Length; i++)
                {
                    tmp[i] = At(i + sourceIndex);
                }
                for (int i = 0; i < tmp.Length; i++)
                {
                    At(i + targetIndex, tmp[i]);
                }

                return;
            }

            for (int i = sourceIndex, ii = targetIndex; i < sourceIndex + count; i++, ii++)
            {
                target.At(ii, At(i));
            }
        }
    }
}
