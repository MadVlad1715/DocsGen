﻿using Core.Entities.Base;
using Core.Exceptions;
using Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : Entity, new()
    {
        protected readonly UniversityContext _context;
        protected readonly DbSet<T> _set;

        public Repository(UniversityContext context)
        {
            _context = context;
            _set = _context.Set<T>();
        }

        public async Task<T> GetById(int entityId)
        {
            var entity = await _set.FindAsync(entityId);

            if (entity == null)
            {
                throw new EntityNotFoundException("Entity not found.", typeof(T));
            }

            return entity;
        }

        public async Task<IEnumerable<T>> GetAll()
        {
            var entities = await _set.ToListAsync();

            return entities;
        }

        public void Add(T entity)
        {
            _set.Add(entity);
        }

        public void Update(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
        }

        public void Delete(int entityId)
        {
            Delete(new T { Id = entityId });
        }

        public void Delete(T entity)
        {
            _set.Remove(entity);
        }

        public async Task<bool> Exists(int entityId)
        {
            var exists = await _set.AnyAsync(entity => entity.Id == entityId);

            return exists;
        }

        public async Task<bool> Exists(T entity)
        {
            var exists = await Exists(entity.Id);

            return exists;
        }
    }
}
