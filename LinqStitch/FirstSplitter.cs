using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LinqStitch
{
    class FirstSplitter : ExpressionVisitor
    {
        DataContext context;

        public FirstSplitter(DataContext context, int splitAt)
        {
            this.context = context;
            ChainLength = 0;
            SplitPoint = splitAt;
            CurrentLink = 0;
            ExprList = new();
            //NewSource = newSource;
        }

        public Expression Execute(Expression expr)
        {
            Expression newExpr = this.Visit(expr);

            return newExpr;
        }

        public object NewSource { get; }
        public List<Expression> ExprList { get; private set; }
        public DataSet BaseDataSet { get; private set; }

        public int ChainLength { get; private set; }

        public int SplitPoint { get; private set; }

        public Expression Before { get; private set; }

        public Expression After { get; private set; }

        private int CurrentLink { get; set; }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            ChainLength++;
            CurrentLink++;

            if (ChainLength == SplitPoint)
            {
                Before = m.Arguments.First();
            }

            var newArgs = m.Arguments.Select(arg => this.Visit(arg)).ToArray();
            var newInstance = this.Visit(m.Object);

            CurrentLink--;
            Expression ret = Expression.Call(newInstance, m.Method, newArgs);
            ExprList.Add(ret);
            return ret;
        }

        //protected override Expression VisitConstant(ConstantExpression node)
        //{
        //    if (node.Value is DataSet dataSet && dataSet.IsBase)
        //    {
        //        ExprList.Add(node);
        //        BaseDataSet = dataSet;
        //        return Expression.Constant(dataSet.Source);
        //    }

        //    return node;
        //}

        //protected override Expression VisitMember(MemberExpression node)
        //{
        //    // check whether we're accessing a property that crosses data source boundaries
        //    if (node.Member is PropertyInfo property)
        //    {
        //        if (context.HasBoundaryProperty(property))
        //        {
        //            SplitPoint = CurrentLink;
        //        }
        //    }


        //    return node;
        //}
    }
}
