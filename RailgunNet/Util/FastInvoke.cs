using System;
using System.Linq.Expressions;
using System.Reflection;
using RailgunNet.System.Encoding;

namespace RailgunNet.Util
{
    public static class FastInvoke
    {
        /// <summary>
        ///     https://stackoverflow.com/questions/17660097/is-it-possible-to-speed-this-method-up/17669142#17669142
        /// </summary>
        public static Func<T, object> BuildUntypedGetter<T>(MemberInfo memberInfo)
        {
            Type targetType = memberInfo.DeclaringType;
            ParameterExpression exInstance = Expression.Parameter(targetType, "t");

            MemberExpression exMemberAccess =
                Expression.MakeMemberAccess(exInstance, memberInfo); // t.PropertyName
            UnaryExpression exConvertToObject =
                Expression.Convert(
                    exMemberAccess,
                    typeof(object)); // Convert(t.PropertyName, typeof(object))
            Expression<Func<T, object>> lambda =
                Expression.Lambda<Func<T, object>>(exConvertToObject, exInstance);

            Func<T, object> action = lambda.Compile();
            return action;
        }

        /// <summary>
        ///     https://stackoverflow.com/questions/17660097/is-it-possible-to-speed-this-method-up/17669142#17669142
        /// </summary>
        public static Action<T, object> BuildUntypedSetter<T>(MemberInfo memberInfo)
        {
            Type targetType = memberInfo.DeclaringType;
            ParameterExpression exInstance = Expression.Parameter(targetType, "t");

            MemberExpression exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);

            // t.PropertValue(Convert(p))
            ParameterExpression exValue = Expression.Parameter(typeof(object), "p");
            UnaryExpression exConvertedValue =
                Expression.Convert(exValue, GetUnderlyingType(memberInfo));
            BinaryExpression exBody = Expression.Assign(exMemberAccess, exConvertedValue);

            Expression<Action<T, object>> lambda =
                Expression.Lambda<Action<T, object>>(exBody, exInstance, exValue);
            Action<T, object> action = lambda.Compile();
            return action;
        }

        public static Func<RailBitBuffer, object> BuildDecodeCall(MethodInfo method)
        {
            Type targetType = method.DeclaringType;
            ParameterExpression exInstance = Expression.Parameter(targetType, "t");

            MethodCallExpression exBody = Expression.Call(exInstance, method);
            UnaryExpression exConvertToObject = Expression.Convert(exBody, typeof(object));

            Expression<Func<RailBitBuffer, object>> lambda =
                Expression.Lambda<Func<RailBitBuffer, object>>(exConvertToObject, exInstance);
            return lambda.Compile();
        }

        public static Action<RailBitBuffer, object> BuildEncodeCall(MethodInfo method)
        {
            Type targetType = method.DeclaringType;
            ParameterExpression exInstance = Expression.Parameter(targetType, "t");
            ParameterExpression exParameter0 = Expression.Parameter(typeof(object), "p");
            UnaryExpression exConvertParam0 = Expression.Convert(
                exParameter0,
                method.GetParameters()[0].ParameterType);
            MethodCallExpression exBody = Expression.Call(exInstance, method, exConvertParam0);
            Expression<Action<RailBitBuffer, object>> lambda =
                Expression.Lambda<Action<RailBitBuffer, object>>(exBody, exInstance, exParameter0);
            return lambda.Compile();
        }

        public static Func<RailBitBuffer, object> BuildDecodeCall(
            MethodInfo method,
            object compressor)
        {
            ConstantExpression exCompressor = Expression.Constant(compressor);
            ParameterExpression exParameter0 = Expression.Parameter(
                typeof(RailBitBuffer),
                "buffer");

            MethodCallExpression exBody = Expression.Call(exCompressor, method, exParameter0);
            UnaryExpression exConvertToObject = Expression.Convert(exBody, typeof(object));

            Expression<Func<RailBitBuffer, object>> lambda =
                Expression.Lambda<Func<RailBitBuffer, object>>(exConvertToObject, exParameter0);
            return lambda.Compile();
        }

        public static Action<RailBitBuffer, object> BuildEncodeCall(
            MethodInfo method,
            object compressor)
        {
            ConstantExpression exCompressor = Expression.Constant(compressor);
            ParameterExpression exParameter0 = Expression.Parameter(
                typeof(RailBitBuffer),
                "buffer");
            ParameterExpression exParameter1 = Expression.Parameter(typeof(object), "p1");
            UnaryExpression exConvertParam1 = Expression.Convert(
                exParameter1,
                method.GetParameters()[1].ParameterType);
            MethodCallExpression exBody = Expression.Call(
                exCompressor,
                method,
                exParameter0,
                exConvertParam1);
            Expression<Action<RailBitBuffer, object>> lambda =
                Expression.Lambda<Action<RailBitBuffer, object>>(
                    exBody,
                    exParameter0,
                    exParameter1);
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
