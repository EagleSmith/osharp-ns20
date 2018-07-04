﻿// -----------------------------------------------------------------------
//  <copyright file="DataAuthCacheBase.cs" company="OSharp开源团队">
//      Copyright (c) 2014-2018 OSharp. All rights reserved.
//  </copyright>
//  <site>http://www.osharp.org</site>
//  <last-editor>郭明锋</last-editor>
//  <last-date>2018-07-04 17:33</last-date>
// -----------------------------------------------------------------------

using System;
using System.Linq;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OSharp.Caching;
using OSharp.Core.EntityInfos;
using OSharp.Dependency;
using OSharp.Entity;
using OSharp.Extensions;
using OSharp.Filter;
using OSharp.Identity;
using OSharp.Secutiry;


namespace OSharp.Security
{
    /// <summary>
    /// 数据权限缓存基类
    /// </summary>
    public abstract class DataAuthCacheBase<TEntityRole, TRole, TEntityInfo, TRoleKey> : IDataAuthCache
        where TEntityRole : EntityRoleBase<TRoleKey>
        where TRole : RoleBase<TRoleKey>
        where TEntityInfo : class, IEntityInfo
        where TRoleKey : IEquatable<TRoleKey>
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger _logger;

        /// <summary>
        /// 初始化一个<see cref="DataAuthCacheBase"/>类型的新实例
        /// </summary>
        protected DataAuthCacheBase()
        {
            _cache = ServiceLocator.Instance.GetService<IDistributedCache>();
            _logger = ServiceLocator.Instance.GetLogger(GetType());
        }

        /// <summary>
        /// 创建数据权限缓存
        /// </summary>
        public void BuildCaches()
        {
            var entityRoles = ServiceLocator.Instance.ExcuteScopedWork(provider =>
            {
                IRepository<TEntityRole, Guid> entityRoleRepository = provider.GetService<IRepository<TEntityRole, Guid>>();
                IRepository<TRole, TRoleKey> roleRepository = provider.GetService<IRepository<TRole, TRoleKey>>();
                IRepository<TEntityInfo, Guid> entityInfoRepository = provider.GetService<IRepository<TEntityInfo, Guid>>();
                return entityRoleRepository.Entities.Where(m => !m.IsLocked).Select(m => new
                {
                    m.FilterGroupJson,
                    RoleName = roleRepository.Entities.First(n => n.Id.Equals(m.RoleId)).Name,
                    EntityTypeFullName = entityInfoRepository.Entities.First(n => n.Id == m.EntityId).TypeName
                }).ToArray();
            });

            foreach (var entityRole in entityRoles)
            {
                FilterGroup filterGroup = entityRole.FilterGroupJson.FromJsonString<FilterGroup>();
                string key = $"Security_EntityRole_{entityRole.RoleName}_{entityRole.EntityTypeFullName}";
                _cache.Set(key, filterGroup);
                _logger.LogDebug($"创建角色“{entityRole.RoleName}”和实体“{entityRole.EntityTypeFullName}”的数据权限规则");
            }
            _logger.LogInformation($"数据权限：创建{entityRoles.Length}个数据权限过滤规则");
        }

        /// <summary>
        /// 移除指定角色名与实体类型的缓存项
        /// </summary>
        /// <param name="roleName">角色名称</param>
        /// <param name="entityTypeFullName">实体类型名称</param>
        public void RemoveCache(string roleName, string entityTypeFullName)
        {
            string key = $"Security_EntityRole_{roleName}_{entityTypeFullName}";
            _cache.Remove(key);
            _logger.LogDebug($"移除角色“{roleName}”和实体“{entityTypeFullName}”的数据权限规则");
        }

        /// <summary>
        /// 获取指定角色名与实体类型的数据权限过滤规则
        /// </summary>
        /// <param name="roleName">角色名称</param>
        /// <param name="entityTypeFullName">实体类型名称</param>
        /// <returns>数据过滤条件组</returns>
        public FilterGroup GetFilterGroup(string roleName, string entityTypeFullName)
        {
            string key = $"Security_EntityRole_{roleName}_{entityTypeFullName}";
            return _cache.Get<FilterGroup>(key);
        }
    }
}