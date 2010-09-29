﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Apics.Utilities.Extension
{
    public sealed class ZipEntry<T1, T2>
    {
        public ZipEntry( int index, T1 value1, T2 value2 )
        {
            Index = index;
            Value1 = value1;
            Value2 = value2;
        }

        public int Index { get; private set; }
        public T1 Value1 { get; private set; }
        public T2 Value2 { get; private set; }
    }

    public static class EnumerableExtensions
    {
        #region [ Zip Extensions ]

        public static IEnumerable<Tuple<TFirst, TSecond>> Zip<TFirst, TSecond>( this IEnumerable<TFirst> first,
            IEnumerable<TSecond> second )
        {
            first.ThrowIfNull( "first" );
            second.ThrowIfNull( "second" );

            return ZipImpl( first, second,
                ( f, s ) => new Tuple<TFirst, TSecond>( f, s ), ImbalancedZipStrategy.Fail );
        }

        /// <summary>
        /// Returns a projection of tuples, where each tuple contains the N-th element 
        /// from each of the argument sequences.
        /// </summary>
        /// <remarks>
        /// If the two input sequences are of different lengths, the result sequence 
        /// is terminated as soon as the shortest input sequence is exhausted.
        /// This operator uses deferred execution and streams its results.
        /// </remarks>
        /// <example>
        /// <code>
        /// int[] numbers = { 1, 2, 3 };
        /// string[] letters = { "A", "B", "C", "D" };
        /// var zipped = numbers.Zip(letters, (n, l) => n + l);
        /// </code>
        /// The <c>zipped</c> variable, when iterated over, will yield "1A", "2B", "3C", in turn.
        /// </example>
        /// <typeparam name="TFirst">Type of elements in first sequence</typeparam>
        /// <typeparam name="TSecond">Type of elements in second sequence</typeparam>
        /// <typeparam name="TResult">Type of elements in result sequence</typeparam>
        /// <param name="first">First sequence</param>
        /// <param name="second">Second sequence</param>
        /// <param name="resultSelector">Function to apply to each pair of elements</param>
        public static IEnumerable<TResult> Zip<TFirst, TSecond, TResult>( this IEnumerable<TFirst> first,
            IEnumerable<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector )
        {
            first.ThrowIfNull( "first" );
            second.ThrowIfNull( "second" );
            resultSelector.ThrowIfNull( "resultSelector" );

            return ZipImpl( first, second, resultSelector, ImbalancedZipStrategy.Truncate );
        }

        /// <summary>
        /// Returns a projection of tuples, where each tuple contains the N-th element 
        /// from each of the argument sequences.
        /// </summary>
        /// <remarks>
        /// If the two input sequences are of different lengths then 
        /// <see cref="InvalidOperationException"/> is thrown.
        /// This operator uses deferred execution and streams its results.
        /// </remarks>
        /// <example>
        /// <code>
        /// int[] numbers = { 1, 2, 3, 4 };
        /// string[] letters = { "A", "B", "C", "D" };
        /// var zipped = numbers.EquiZip(letters, (n, l) => n + l);
        /// </code>
        /// The <c>zipped</c> variable, when iterated over, will yield "1A", "2B", "3C", "4D" in turn.
        /// </example>
        /// <typeparam name="TFirst">Type of elements in first sequence</typeparam>
        /// <typeparam name="TSecond">Type of elements in second sequence</typeparam>
        /// <typeparam name="TResult">Type of elements in result sequence</typeparam>
        /// <param name="first">First sequence</param>
        /// <param name="second">Second sequence</param>
        /// <param name="resultSelector">Function to apply to each pair of elements</param>
        public static IEnumerable<TResult> EquiZip<TFirst, TSecond, TResult>( this IEnumerable<TFirst> first,
            IEnumerable<TSecond> second,
            Func<TFirst, TSecond, TResult> resultSelector )
        {
            first.ThrowIfNull( "first" );
            second.ThrowIfNull( "second" );
            resultSelector.ThrowIfNull( "resultSelector" );

            return ZipImpl( first, second, resultSelector, ImbalancedZipStrategy.Fail );
        }

        /// <summary>
        /// Returns a projection of tuples, where each tuple contains the N-th element 
        /// from each of the argument sequences.
        /// </summary>
        /// <remarks>
        /// If the two input sequences are of different lengths then the result 
        /// sequence will always be as long as the longer of the two input sequences.
        /// The default value of the shorter sequence element type is used for padding.
        /// This operator uses deferred execution and streams its results.
        /// </remarks>
        /// <example>
        /// <code>
        /// int[] numbers = { 1, 2, 3 };
        /// string[] letters = { "A", "B", "C", "D" };
        /// var zipped = numbers.EquiZip(letters, (n, l) => n + l);
        /// </code>
        /// The <c>zipped</c> variable, when iterated over, will yield "1A", "2B", "3C", "0D" in turn.
        /// </example>
        /// <typeparam name="TFirst">Type of elements in first sequence</typeparam>
        /// <typeparam name="TSecond">Type of elements in second sequence</typeparam>
        /// <typeparam name="TResult">Type of elements in result sequence</typeparam>
        /// <param name="first">First sequence</param>
        /// <param name="second">Second sequence</param>
        /// <param name="resultSelector">Function to apply to each pair of elements</param>
        public static IEnumerable<TResult> ZipLongest<TFirst, TSecond, TResult>( this IEnumerable<TFirst> first,
            IEnumerable<TSecond> second,
            Func<TFirst, TSecond, TResult> resultSelector )
        {
            first.ThrowIfNull( "first" );
            second.ThrowIfNull( "second" );
            resultSelector.ThrowIfNull( "resultSelector" );

            return ZipImpl( first, second, resultSelector, ImbalancedZipStrategy.Pad );
        }

        private static IEnumerable<TResult> ZipImpl<TFirst, TSecond, TResult>(
            IEnumerable<TFirst> first,
            IEnumerable<TSecond> second,
            Func<TFirst, TSecond, TResult> resultSelector,
            ImbalancedZipStrategy imbalanceStrategy )
        {
            using( IEnumerator<TFirst> e1 = first.GetEnumerator( ) )
            {
                using( IEnumerator<TSecond> e2 = second.GetEnumerator( ) )
                {
                    while( e1.MoveNext( ) )
                    {
                        if( e2.MoveNext( ) )
                        {
                            yield return resultSelector( e1.Current, e2.Current );
                        }
                        else
                        {
                            switch( imbalanceStrategy )
                            {
                                case ImbalancedZipStrategy.Fail:
                                    throw new InvalidOperationException( "Second sequence ran out before first" );
                                case ImbalancedZipStrategy.Truncate:
                                    yield break;
                                case ImbalancedZipStrategy.Pad:
                                    do
                                    {
                                        yield return resultSelector( e1.Current, default( TSecond ) );
                                    } while( e1.MoveNext( ) );
                                    yield break;
                            }
                        }
                    }

                    if( !e2.MoveNext( ) )
                        yield break;
                    
                    switch( imbalanceStrategy )
                    {
                        case ImbalancedZipStrategy.Fail:
                            throw new InvalidOperationException( "First sequence ran out before second" );
                        case ImbalancedZipStrategy.Truncate:
                            break;
                        case ImbalancedZipStrategy.Pad:
                            do
                            {
                                yield return resultSelector( default( TFirst ), e2.Current );
                            } while( e2.MoveNext( ) );
                            break;
                    }
                }
            }
        }

        #endregion [ Zip Extensions ]

        #region Nested type: ImbalancedZipStrategy

        /// <summary>
        /// Strategy determining the handling of the case where the inputs are of
        /// unequal lengths.
        /// </summary>
        internal enum ImbalancedZipStrategy
        {
            /// <summary>
            /// The result sequence ends when either input sequence is exhausted.
            /// </summary>
            Truncate = 0,
            /// <summary>
            /// The result sequence ends when both sequences are exhausted. The 
            /// shorter sequence is effectively "padded" at the end with the default
            /// value for its element type.
            /// </summary>
            Pad = 1,
            /// <summary>
            /// <see cref="InvalidOperationException" /> is thrown if one sequence
            /// is exhausted but not the other.
            /// </summary>
            Fail = 2
        }

        #endregion
    }
}