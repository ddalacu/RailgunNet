using System;
using System.Linq.Expressions;
using System.Reflection;

namespace RailgunNet.Util
{
    public static class InvokableFactory
    {
        /// <summary>
        ///     Returns an untyped getter for a property or field in an instance.
        ///     https://stackoverflow.com/questions/17660097/is-it-possible-to-speed-this-method-up/17669142#17669142
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="memberInfo"></param>
        /// <returns></returns>
        public static Func<TDeclaring, object> CreateUntypedGetter<TDeclaring>(MemberInfo memberInfo)
        {
            Type targetType = memberInfo.DeclaringType;
            if (targetType != typeof(TDeclaring))
            {
                throw new ArgumentException(
                    $"Generic type {typeof(TDeclaring)} does not match declaring type in member info {targetType}.",
                    nameof(memberInfo));
            }

            ParameterExpression exInstance = Expression.Parameter(targetType, "t");
            MemberExpression exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);

            UnaryExpression exConvertToObject = Expression.Convert(exMemberAccess, typeof(object));
            Expression<Func<TDeclaring, object>> lambda =
                Expression.Lambda<Func<TDeclaring, object>>(exConvertToObject, exInstance);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns an untyped setter for a property or field in an instance.
        ///     https://stackoverflow.com/questions/17660097/is-it-possible-to-speed-this-method-up/17669142#17669142
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="memberInfo"></param>
        /// <returns></returns>
        public static Action<TDeclaring, object> CreateUntypedSetter<TDeclaring>(
            MemberInfo memberInfo)
        {
            Type targetType = memberInfo.DeclaringType;
            if (targetType != typeof(TDeclaring))
            {
                throw new ArgumentException(
                    $"Generic type {typeof(TDeclaring)} does not match declaring type in member info {targetType}.",
                    nameof(memberInfo));
            }

            ParameterExpression exInstance = Expression.Parameter(targetType, "t");
            MemberExpression exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);

            ParameterExpression exParameter0 = Expression.Parameter(typeof(object), "p");
            UnaryExpression exConvertToUnderlying = Expression.Convert(
                exParameter0,
                GetUnderlyingType(memberInfo));
            BinaryExpression exAssign = Expression.Assign(exMemberAccess, exConvertToUnderlying);
            Expression<Action<TDeclaring, object>> lambda =
                Expression.Lambda<Action<TDeclaring, object>>(exAssign, exInstance, exParameter0);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns an member method call of the form `object Method(TDeclaring)`.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="method"></param>
        /// <returns></returns>
        public static Func<TDeclaring, object> CreateCallWithReturn<TDeclaring>(MethodInfo method)
        {
            Type targetType = method.DeclaringType;
            if (targetType != typeof(TDeclaring))
            {
                throw new ArgumentException(
                    $"Generic type {typeof(TDeclaring)} does not match declaring type in member info {targetType}.",
                    nameof(method));
            }

            ParameterExpression exInstance = Expression.Parameter(targetType, "t");
            MethodCallExpression exCall = Expression.Call(exInstance, method);
            UnaryExpression exConvertToObject = Expression.Convert(exCall, typeof(object));

            Expression<Func<TDeclaring, object>> lambda =
                Expression.Lambda<Func<TDeclaring, object>>(exConvertToObject, exInstance);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns a member method call of the form `void Method(TDeclaring, object)`.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="method"></param>
        /// <returns></returns>
        public static Action<TDeclaring, object> CreateCall<TDeclaring>(MethodInfo method)
        {
            Type targetType = method.DeclaringType;
            if (targetType != typeof(TDeclaring))
            {
                throw new ArgumentException(
                    $"Generic type {typeof(TDeclaring)} does not match declaring type in member info {targetType}.",
                    nameof(method));
            }

            ParameterExpression exInstance = Expression.Parameter(targetType, "t");
            ParameterExpression exParameter0 = Expression.Parameter(typeof(object), "p");
            UnaryExpression exConvertToParam0 = Expression.Convert(
                exParameter0,
                method.GetParameters()[0].ParameterType);
            MethodCallExpression exCall = Expression.Call(exInstance, method, exConvertToParam0);
            Expression<Action<TDeclaring, object>> lambda =
                Expression.Lambda<Action<TDeclaring, object>>(exCall, exInstance, exParameter0);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns a member method call on `instance` of the form `object Method(TDeclaring)`.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="method"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Func<TDeclaring, object> CreateCallWithReturn<TDeclaring>(
            MethodInfo method,
            object instance)
        {
            ConstantExpression exInstance = Expression.Constant(instance);
            ParameterExpression exParameter0 = Expression.Parameter(typeof(TDeclaring), "buffer");

            MethodCallExpression exBody = Expression.Call(exInstance, method, exParameter0);
            UnaryExpression exConvertToObject = Expression.Convert(exBody, typeof(object));

            Expression<Func<TDeclaring, object>> lambda =
                Expression.Lambda<Func<TDeclaring, object>>(exConvertToObject, exParameter0);
            return lambda.Compile();
        }

        /// <summary>
        ///     Returns a member method call on `instance`of the form `void Method(TDeclaring, object)`.
        /// </summary>
        /// <typeparam name="TDeclaring">Type of the instance containing the member.</typeparam>
        /// <param name="method"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Action<TDeclaring, object> CreateCall<TDeclaring>(
            MethodInfo method,
            object instance)
        {
            ConstantExpression exInstance = Expression.Constant(instance);
            ParameterExpression exParameter0 = Expression.Parameter(typeof(TDeclaring), "buffer");
            ParameterExpression exParameter1 = Expression.Parameter(typeof(object), "p1");
            UnaryExpression exConvertParam1 = Expression.Convert(
                exParameter1,
                method.GetParameters()[1].ParameterType);
            MethodCallExpression exBody = Expression.Call(
                exInstance,
                method,
                exParameter0,
                exConvertParam1);
            Expression<Action<TDeclaring, object>> lambda =
                Expression.Lambda<Action<TDeclaring, object>>(exBody, exParameter0, exParameter1);
            return lambda.Compile();
        }

        public static Type GetUnderlyingType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Event:
                    return ((EventInfo) member).EventHandlerType;
                case MemberTypes.Field:
                    return ((FieldInfo) member).FieldType;
                case MemberTypes.Method:
                    return ((MethodInfo) member).ReturnType;
                case MemberTypes.Property:
                    return ((PropertyInfo) member).PropertyType;
                default:
                    throw new ArgumentException(
                        "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo");
            }
        }
    }
}
