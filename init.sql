CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,                        -- уникальный идентификатор пользователя
    username VARCHAR(100) UNIQUE NOT NULL,       -- логин пользователя
    password_hash VARCHAR(256) NOT NULL          -- хэш пароля
);
CREATE TABLE secrets (
    id SERIAL PRIMARY KEY,                        -- уникальный идентификатор секрета
    name VARCHAR(100) NOT NULL,                  -- название секрета
    username VARCHAR(100),                        -- логин для сервиса
    password VARCHAR(256),                        -- пароль для секрета
    description TEXT,                             -- дополнительное описание
    created_at TIMESTAMP DEFAULT NOW()           -- дата создания записи
);