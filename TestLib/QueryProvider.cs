﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace TestLib
{
    public class QueryProvider : IQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
        {
            throw new NotImplementedException("CreateQuery");
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            //var sql = SqlBuilder.Translate(expression);

            return new DbSet<TElement>(this, expression);
        }

        public object Execute(Expression expression)
        {
            throw new NotImplementedException("Execute");
        }

        public TResult Execute<TResult>(Expression expression)
        {
            var sql = SqlBuilder.Translate(expression);

            return default;
        }
    }
}
