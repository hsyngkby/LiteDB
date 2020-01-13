﻿using System;
using System.Collections.Generic;
using System.Linq;
using static LiteDB.Constants;

namespace LiteDB.Engine
{
    /// <summary>
    /// Calculate index cost based on expression/collection index. 
    /// Lower cost is better - lowest will be selected
    /// </summary>
    internal class IndexCost
    {
        public uint Cost { get; }

        /// <summary>
        /// Get filtered expression: "$._id = 10"
        /// </summary>
        public BsonExpression Expression { get; }

        /// <summary>
        /// Get index expression only: "$._id"
        /// </summary>
        public string IndexExpression { get; }

        /// <summary>
        /// Get created Index instance used on query
        /// </summary>
        public Index Index { get; }

        public IndexCost(CollectionIndex index, BsonExpression expr, BsonExpression value, Collation collation)
        {
            // copy root expression parameters to my value expression
            expr.Parameters.CopyTo(value.Parameters);

            this.IndexExpression = index.Expression;
            this.Expression = expr;

            // create index instance
            this.Index = value.Execute(collation).Select(x => this.CreateIndex(expr.Type, index.Name, x)).FirstOrDefault();

            ENSURE(this.Index != null, "index must be not null");

            // calcs index cost
            this.Cost = this.Index.GetCost(index);
        }

        // used when full index search
        public IndexCost(CollectionIndex index)
        {
            this.Expression = BsonExpression.Create(index.Expression);
            this.Index = new IndexAll(index.Name, Query.Ascending);
            this.Cost = this.Index.GetCost(index);
            this.IndexExpression = index.Expression;
        }

        /// <summary>
        /// Create index based on expression predicate
        /// </summary>
        private Index CreateIndex(BsonExpressionType type, string name, BsonValue value)
        {
            switch(type)
            {
                case BsonExpressionType.Equal: return new IndexEquals(name, value);
                case BsonExpressionType.Between: return new IndexRange(name, value.AsArray[0], value.AsArray[1], true, true, Query.Ascending);
                case BsonExpressionType.Like: return new IndexLike(name, value.AsString, Query.Ascending);
                case BsonExpressionType.GreaterThan: return new IndexRange(name, value, BsonValue.MaxValue, false, true, Query.Ascending);
                case BsonExpressionType.GreaterThanOrEqual: return new IndexRange(name, value, BsonValue.MaxValue, true, true, Query.Ascending);
                case BsonExpressionType.LessThan: return new IndexRange(name, BsonValue.MinValue, value, true, false, Query.Ascending);
                case BsonExpressionType.LessThanOrEqual: return new IndexRange(name, BsonValue.MinValue, value, true, true, Query.Ascending);
                case BsonExpressionType.NotEqual: return new IndexScan(name, x => x.CompareTo(value) != 0, Query.Ascending);
                case BsonExpressionType.In: return value.IsArray ?
                        (Index)new IndexIn(name, value.AsArray, Query.Ascending) :
                        (Index)new IndexEquals(name, value);
                default: return null;
            }
        }
    }
}