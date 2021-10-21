using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LinqStitch
{
    class ExpressionRewriter : ExpressionVisitor
    {
        DataContext context;

        public ExpressionRewriter(DataContext context)
        {
            this.context = context;
            ChainLength = 0;
            SplitPoint = -1;
            CurrentLink = 0;
            ExprList = new();
        }

        public Expression Execute(Expression expr)
        {
            Expression newExpr = this.Visit(expr);

            return newExpr;
        }

        public List<Expression> ExprList { get; private set; }
        public DataSet BaseDataSet { get; private set; }

        public  int ChainLength { get; private set; }

        public int SplitPoint { get; private set; }

        private int CurrentLink { get; set; }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            ChainLength++;
            CurrentLink++;
            
            var newArgs = m.Arguments.Select(arg => this.Visit(arg)).ToArray();
            var newInstance = this.Visit(m.Object);

            CurrentLink--;
            Expression ret = Expression.Call(newInstance, m.Method, newArgs);
            ExprList.Add(ret);
            return ret;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is DataSet dataSet && dataSet.IsBase)
            {
                ExprList.Add(node);
                BaseDataSet = dataSet;
                return Expression.Constant(dataSet.Source);
            }

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            MemberExpression expr = node;
            while(expr.Expression.NodeType == ExpressionType.MemberAccess)
            {
                expr = expr.Expression as MemberExpression;
            }
            // check whether we're accessing a property that crosses data source boundaries
            if (expr.Member is PropertyInfo property)
            {
                if (context.HasBoundaryProperty(property))
                {
                    SplitPoint = CurrentLink;
                }
            }


            return node;
        }
    }
}
