﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PageTypeBuilder.Reflection
{
    public static class TypeExtensions
    {
        public static PropertyInfo[] GetPublicOrPrivateProperties(this Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static IEnumerable<PropertyInfo> GetPropertiesFromInterfaces(this Type type)
        {
            var propertiesFromInterfaces = new List<PropertyInfo>();
            foreach (var face in type.GetInterfaces())
            {
                foreach (var propertyInfo in face.GetPublicOrPrivateProperties())
                {
                    propertiesFromInterfaces.Add(propertyInfo);
                }
            }
            return propertiesFromInterfaces;
        }

        internal static IEnumerable<PropertyInfo> GetPageTypePropertiesOnClass(this Type pageTypeType)
        {
            return pageTypeType.GetPublicOrPrivateProperties().Where(propertyInfo => propertyInfo.HasAttribute(typeof(PageTypePropertyAttribute)));
        }

        internal static IEnumerable<PropertyInfo> GetPageTypePropertiesFromInterfaces(this Type pageTypeType)
        {
            return pageTypeType.GetPropertiesFromInterfaces()
                .Where(propertyInfo => propertyInfo.HasAttribute(typeof(PageTypePropertyAttribute)));
        }

        internal static IEnumerable<PropertyInfo> GetAllValidPageTypePropertiesFromClassAndImplementedInterfaces(this Type pageTypeType)
        {
            var pageTypeProperties = pageTypeType.GetPageTypePropertiesOnClass().ToList();
            IEnumerable<PropertyInfo> propertiesFromInterfaces = pageTypeType.GetPageTypePropertiesFromInterfaces();
            foreach (var interfaceProperty in propertiesFromInterfaces)
            {
                if (pageTypeProperties.Count(p => p.Name.Equals(interfaceProperty.Name)) == 0)
                    pageTypeProperties.Add(interfaceProperty);
            }
            return pageTypeProperties;
        }

        public static IEnumerable<Type> AssignableTo(this IEnumerable<Type> types, Type superType)
        {
            return types.Where(superType.IsAssignableFrom);
        }

        public static IEnumerable<Type> Concrete(this IEnumerable<Type> types)
        {
            return types.Where(type => !type.IsAbstract);
        }

        public static bool IsNullableType(this Type type)
        {
            return (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)));
        }

        public static bool CanBeNull(this Type type)
        {
            return !type.IsValueType || type.IsNullableType();
        }
    }
}
