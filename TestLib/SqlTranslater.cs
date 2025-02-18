﻿using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace TestLib
{
    public class SqlTranslater : ExpressionVisitor
    {
        private StringBuilder sb;
        private string _orderBy = string.Empty;
        private int? _skip = null;
        private int? _take = null;
        private string _whereClause = string.Empty;
        private string _selectClause = string.Empty;

        public int? Skip => _skip;
        public int? Take => _take;
        public string OrderBy => _orderBy;
        public string WhereClause => _whereClause;
        public string SelectClause => _selectClause;

        public string Translate(Expression expression)
        {
            this.sb = new StringBuilder();
            this.Visit(expression);
            _whereClause = this.sb.ToString();
            return $"SELECT {_selectClause} FROM ---- WHERE {_whereClause} ORDER BY {_orderBy}";
        }

        private static Expression StripQuotes(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
            {
                e = ((UnaryExpression)e).Operand;
            }
            return e;
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.DeclaringType == typeof(Queryable) && m.Method.Name == "Where")
            {
                this.Visit(m.Arguments[0]);
                LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                this.Visit(lambda.Body);
                return m;
            }
            else if (m.Method.Name == "Take")
            {
                if (this.ParseTakeExpression(m))
                {
                    Expression nextExpression = m.Arguments[0];
                    return this.Visit(nextExpression);
                }
            }
            else if (m.Method.Name == "Skip")
            {
                if (this.ParseSkipExpression(m))
                {
                    Expression nextExpression = m.Arguments[0];
                    return this.Visit(nextExpression);
                }
            }
            else if (m.Method.Name == "OrderBy")
            {
                if (this.ParseOrderByExpression(m, "ASC"))
                {
                    Expression nextExpression = m.Arguments[0];
                    return this.Visit(nextExpression);
                }
            }
            else if (m.Method.Name == "OrderByDescending")
            {
                if (this.ParseOrderByExpression(m, "DESC"))
                {
                    Expression nextExpression = m.Arguments[0];
                    return this.Visit(nextExpression);
                }
            }
            else if (m.Method.Name == "Select")
            {
                if (this.ParseSelectExpression(m))
                {
                    Expression nextExpression = m.Arguments[0];
                    return this.Visit(nextExpression);
                }
            }
            else if (m.Method.Name == "Join")
            {
                if (this.ParseJoinExpression(m))
                {
                    Expression nextExpression = m.Arguments[0];
                    return this.Visit(nextExpression);
                }
            }

             return Expression.Empty();
            //throw new NotSupportedException(string.Format("The method '{0}' is not supported", m.Method.Name));
        }


        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    sb.Append(" NOT ");
                    this.Visit(u.Operand);
                    break;
                case ExpressionType.Convert:
                    this.Visit(u.Operand);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
            }
            return u;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            sb.Append("(");
            this.Visit(b.Left);

            switch (b.NodeType)
            {
                case ExpressionType.And:
                    sb.Append(" AND ");
                    break;

                case ExpressionType.AndAlso:
                    sb.Append(" AND ");
                    break;

                case ExpressionType.Or:
                    sb.Append(" OR ");
                    break;

                case ExpressionType.OrElse:
                    sb.Append(" OR ");
                    break;

                case ExpressionType.Equal:
                    if (IsNullConstant(b.Right))
                    {
                        sb.Append(" IS ");
                    }
                    else
                    {
                        sb.Append(" = ");
                    }
                    break;

                case ExpressionType.NotEqual:
                    if (IsNullConstant(b.Right))
                    {
                        sb.Append(" IS NOT ");
                    }
                    else
                    {
                        sb.Append(" <> ");
                    }
                    break;

                case ExpressionType.LessThan:
                    sb.Append(" < ");
                    break;

                case ExpressionType.LessThanOrEqual:
                    sb.Append(" <= ");
                    break;

                case ExpressionType.GreaterThan:
                    sb.Append(" > ");
                    break;

                case ExpressionType.GreaterThanOrEqual:
                    sb.Append(" >= ");
                    break;

                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));

            }

            this.Visit(b.Right);
            sb.Append(")");
            return b;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            IQueryable q = c.Value as IQueryable;

            if (q == null && c.Value == null)
            {
                sb.Append("NULL");
            }
            else if (q == null)
            {
                switch (Type.GetTypeCode(c.Value.GetType()))
                {
                    case TypeCode.Boolean:
                        sb.Append(((bool)c.Value) ? 1 : 0);
                        break;

                    case TypeCode.String:
                        sb.Append("'");
                        sb.Append(c.Value);
                        sb.Append("'");
                        break;

                    case TypeCode.DateTime:
                        sb.Append("'");
                        sb.Append(c.Value);
                        sb.Append("'");
                        break;

                    case TypeCode.Object:
                        throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", c.Value));

                    default:
                        sb.Append(c.Value);
                        break;
                }
            }

            return c;
        }

        protected override Expression VisitMember(MemberExpression m)
        {
            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
            {
                sb.Append(m.ToString());
                return m;
            }

            if (m.Expression != null && m.Expression.NodeType == ExpressionType.MemberAccess)
            {
                MemberExpression me = (MemberExpression)m.Expression;
                sb.Append($"[{me.Member.Name}].[{m.Member.Name}]");
                return m;
            }

            if (m.ToString() == "String.Empty")
            {
                sb.Append("' '");
                return m;
            }
            throw new NotSupportedException(string.Format("The member '{0}' is not supported", m.Member.Name));
        }

        protected bool IsNullConstant(Expression exp)
        {
            return (exp.NodeType == ExpressionType.Constant && ((ConstantExpression)exp).Value == null);
        }

        private bool ParseOrderByExpression(MethodCallExpression expression, string order)
        {
            UnaryExpression unary = (UnaryExpression)expression.Arguments[1];
            LambdaExpression lambdaExpression = (LambdaExpression)unary.Operand;

            MemberExpression body = lambdaExpression.Body as MemberExpression;
            if (body != null)
            {
                MemberExpression inner = (MemberExpression)body.Expression;
                if (string.IsNullOrEmpty(_orderBy))
                {
                    _orderBy = string.Format("[{0}].[{1}] {2}", inner.Member.Name, body.Member.Name, order);
                }
                else
                {
                    _orderBy = string.Format("[{0}].[{1}] {2} {3}", _orderBy, inner.Member.Name, body.Member.Name, order);
                }

                return true;
            }

            return false;
        }

        private bool ParseTakeExpression(MethodCallExpression expression)
        {
            ConstantExpression sizeExpression = (ConstantExpression)expression.Arguments[1];

            int size;
            if (int.TryParse(sizeExpression.Value.ToString(), out size))
            {
                _take = size;
                return true;
            }

            return false;
        }

        private bool ParseSkipExpression(MethodCallExpression expression)
        {
            ConstantExpression sizeExpression = (ConstantExpression)expression.Arguments[1];

            int size;
            if (int.TryParse(sizeExpression.Value.ToString(), out size))
            {
                _skip = size;
                return true;
            }

            return false;
        }


        private bool ParseSelectExpression(MethodCallExpression m)
        {
            UnaryExpression unary = (UnaryExpression)m.Arguments[1];
            LambdaExpression lambdaExpression = (LambdaExpression)unary.Operand;
            NewExpression newExpression = (NewExpression)lambdaExpression.Body;
            var result = newExpression.Arguments
                .Where(e => e.NodeType == ExpressionType.MemberAccess)
                .Select(e =>
                {
                    var me = (MemberExpression)e;
                    string propertyName = me.Member.Name;

                    var inner = (MemberExpression)me.Expression;
                    string typeAlias = inner.Member.Name;

                    return $"[{typeAlias}].[{propertyName}]";
                });
            _selectClause = string.Join(", ", result);
                
            return true;
        }

        private bool ParseJoinExpression(MethodCallExpression m)
        {
            return true;
        }
    }
}