using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LinqStitch
{
    public abstract class DataContext : IQueryProvider
    {

        public DataContext()
        {
            DataContextBuilder builder = new DataContextBuilder(this);
            OnConfiguring(builder);
            Config = builder;

            // initialize data sets
            foreach(var entry in Config.DataSets)
            {
                // retrieve data set source from config
                PropertyInfo property = this.GetType().GetProperty(entry.Key);
                var elemType = property.PropertyType.GetGenericArguments()[0];
                Type dsBuilderType = typeof(DataSetBuilder<>).MakeGenericType(elemType);
                object dsConf = entry.Value;

                object dsSource = dsBuilderType.GetProperty("Source").GetValue(dsConf);

                // create data set using source
                Type dsType = typeof(BaseDataSet<>).MakeGenericType(elemType);
                object dataSet = Activator.CreateInstance(dsType, new[] { this, dsSource });

                property.SetValue(this, dataSet);
            }
        }

        internal DataContextBuilder Config { get; set; }

        protected abstract void OnConfiguring(DataContextBuilder builder);

        public bool HasBoundaryProperty(PropertyInfo property)
        {
            foreach (var entry in Config.DataSets)
            {
                // check whether there's a dataset config that has a configuration for this foreign property
                PropertyInfo dsProperty = this.GetType().GetProperty(entry.Key);
                if (property.DeclaringType != TypeHelper.GetElementType(dsProperty.PropertyType))
                {
                    continue;
                }

                var elemType = dsProperty.PropertyType.GetGenericArguments()[0];
                Type dsBuilderType = typeof(DataSetBuilder<>).MakeGenericType(elemType);
                object dsConf = entry.Value;

                var dsProperties = (Dictionary<string, object>)dsBuilderType.GetProperty("PropertyConfigs").GetValue(dsConf);

                if (dsProperties.ContainsKey(property.Name))
                {
                    return true;
                }
            }

            return false;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            Type elementType = TypeHelper.GetElementType(expression.Type);

            try
            {

                return (IQueryable)Activator.CreateInstance(typeof(DerivedDataSet<>).MakeGenericType(elementType), new object[] { this, expression });

            }

            catch (TargetInvocationException tie)
            {

                throw tie.InnerException;

            }
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new DerivedDataSet<TElement>(this, expression);
        }

        public object Execute(Expression expression)
        {
            var rewriter = new ExpressionRewriter(this);
            var newExpr = rewriter.Execute(expression);
            var source = rewriter.BaseDataSet.Source;

            if (rewriter.SplitPoint == -1)
            {
                // no cross-boundary data access detected

                if (newExpr.Type.IsGenericType) // TODO: quick hacky way to check if it's IEnumerable<T>/IQueryable<T>
                {
                    var toListMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(rewriter.BaseDataSet.Source.ElementType);
                    var result = source.Provider.CreateQuery(newExpr);
                    var resultList = toListMethod.Invoke(null, new[] { result });
                    return resultList;
                }
                else
                {
                    return source.Provider.Execute(newExpr);
                }
            }

            // cross-boundary access detected at split point

            var firstSplitter = new FirstSplitter(this, rewriter.SplitPoint);
       
            
            // TODO: split point might be > array length? (at which point we should use the source as the splitExpr)
            //Expression splitExpr = rewriter.ExprList[rewriter.ExprList.Count - rewriter.SplitPoint - 1];

            firstSplitter.Execute(expression);
            Expression splitExpr = firstSplitter.Before;

            // fetch pre-split query and execute the remainder of the query in-memory
            IQueryable halfResult = this.CreateQuery(splitExpr);
            //object halfResult = this.Execute(splitExpr);


            //IEnumerable halfEnumerable = halfResult as IEnumerable;
            // TODO: this shouldn't happen, right?
            //if (halfEnumerable == null)
            //{
            //    return halfResult;
            //}

            var toList = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(halfResult.ElementType);
            var halfList = toList.Invoke(null, new[] { halfResult });



            var populateMethod = GetType().GetMethod("PopulateObjects").MakeGenericMethod(TypeHelper.GetElementType(halfResult.GetType()));
            var populatedHalfResult = (populateMethod.Invoke(this, new[] { halfList }) as IEnumerable).AsQueryable();
            //var toList = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(populatedHalfResult.ElementType);
            //var halfList = toList.Invoke(null, new[] { populatedHalfResult });
            //IQueryable populateHalfdResult = PopulateObjects(halfEnumerable).AsQueryable();

            var populatedHalfList = toList.Invoke(null, new[] { populatedHalfResult });
            var asQueryable = typeof(Queryable)
                .GetMethods().FirstOrDefault(m => m.IsGenericMethod && m.Name == "AsQueryable")
                .MakeGenericMethod(halfResult.ElementType);
            var populatedQueryable = asQueryable.Invoke(null, new[] { populatedHalfList });
            var splitter = new ExpressionSplitter(this, populatedQueryable, rewriter.SplitPoint);
            var finalExpr = splitter.Execute(expression);


            return (populatedQueryable as IQueryable).Provider.Execute(finalExpr);
        }

        public IEnumerable<T> PopulateObjects<T>(IEnumerable sourceSet)
        {
            foreach(object source in sourceSet)
            {
                yield return (T)PopulateObject(source);
            }
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return (TResult)this.Execute(expression);
        }

        private object PopulateObject(object source)
        {
            Type type = source.GetType();
            var property = GetType().GetProperties().FirstOrDefault(p => TypeHelper.GetElementType(p.PropertyType) == type);
            if (property == null)
            {
                return source;
            }

            return PopulateObject(source, property.Name);
        }

        private object PopulateObject(object source, string dataSet)
        {
            Type type = TypeHelper.GetElementType(GetType().GetProperty(dataSet).PropertyType);

            // clone source object
            object result = Activator.CreateInstance(type);
            foreach (var property in type.GetProperties())
            {
                property.SetValue(result, property.GetValue(source));
            }

            // populate foreign fields if null
            var contextType = typeof(PropertyFetchContext<>).MakeGenericType(type);
            var context = Activator.CreateInstance(contextType, result);


            Type dsBuilderType = typeof(DataSetBuilder<>).MakeGenericType(type);
            object dsConf = Config.DataSets[dataSet];

            var dsProperties = (Dictionary<string, object>)dsBuilderType.GetProperty("PropertyConfigs").GetValue(dsConf);
            foreach (var entry in dsProperties)
            {
                var property = type.GetProperty(entry.Key);
                if (property.GetValue(result) != null)
                {
                    continue;
                }

                var propConfigType = typeof(PropertyBuilder<,>).MakeGenericType(type, property.PropertyType);
                object propConf = entry.Value;
                MethodInfo fetchFunc = propConfigType.GetMethod("FetchProperty");


                object value = fetchFunc.Invoke(propConf, new[] { context });
                property.SetValue(result, value);
            }

            return result;
        }
    }
}
