using System;
using System.Linq.Expressions;
using System.Reflection;
using RailgunNet.System.Encoding;

namespace RailgunNet.Util
{
    public static class FastInvoke
    {
        /// <summary>
        /// https://stackoverflow.com/questions/17660097/is-it-possible-to-speed-this-method-up/17669142#17669142
        /// </summary>
        public static Func<T, object> BuildUntypedGetter<T>(MemberInfo memberInfo)
        {
            var targetType = memberInfo.DeclaringType;
            var exInstance = Expression.Parameter(targetType, "t");

            var exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);       // t.PropertyName
            var exConvertToObject = Expression.Convert(exMemberAccess, typeof(object));     // Convert(t.PropertyName, typeof(object))
            var lambda = Expression.Lambda<Func<T, object>>(exConvertToObject, exInstance);

            var action = lambda.Compile();
            return action;
        }
        /// <summary>
        /// https://stackoverflow.com/questions/17660097/is-it-possible-to-speed-this-method-up/17669142#17669142
        /// </summary>
        public static Action<T, object> BuildUntypedSetter<T>(MemberInfo memberInfo)
        {
            var targetType = memberInfo.DeclaringType;
            var exInstance = Expression.Parameter(targetType, "t");

            var exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);

            // t.PropertValue(Convert(p))
            var exValue = Expression.Parameter(typeof(object), "p");
            var exConvertedValue = Expression.Convert(exValue, GetUnderlyingType(memberInfo));
            var exBody = Expression.Assign(exMemberAccess, exConvertedValue);

            var lambda = Expression.Lambda<Action<T, object>>(exBody, exInstance, exValue);
            var action = lambda.Compile();
            return action;
        }
        public static Func<RailBitBuffer, object> BuildDecodeCall(MethodInfo method)
        {
            var targetType = method.DeclaringType;
            var exInstance = Expression.Parameter(targetType, "t");

            var exBody = Expression.Call(exInstance, method);
            var exConvertToObject = Expression.Convert(exBody, typeof(object));

            var lambda = Expression.Lambda<Func<RailBitBuffer, object>>(exConvertToObject, exInstance);
            return lambda.Compile();
        }
        public static Action<RailBitBuffer, object> BuildEncodeCall(MethodInfo method)
        {
            var targetType = method.DeclaringType;
            var exInstance = Expression.Parameter(targetType, "t");
            var exParameter0 = Expression.Parameter(typeof(object), "p");
            var exConvertParam0 = Expression.Convert(exParameter0, method.GetParameters()[0].ParameterType);
            var exBody = Expression.Call(exInstance, method, exConvertParam0);
            var lambda = Expression.Lambda<Action<RailBitBuffer, object>>(exBody, exInstance, exParameter0);
            return lambda.Compile();
        }

        public static Type GetUnderlyingType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType;
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                default:
                    throw new ArgumentException
                    (
                     "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"
                    );
            }
        }
    }
}
