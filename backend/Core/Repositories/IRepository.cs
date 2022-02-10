﻿using Core.Entities.Base;

namespace Core.Repositories
{
    public interface IRepository<T> where T : Entity
    {
        Task<T> GetById(int entityId);

        Task<IEnumerable<T>> GetAll();

        void Add(T entity);

        void Update(T entity);

        void Delete(int entityId);

        void Delete(T entity);

        Task<bool> Exists(int entityId);

        Task<bool> Exists(T entity);
    }
}
