# Исправление проблемы с зависанием multipart запросов

## Проблема
POST запросы с multipart/form-data зависают и не доходят до контроллера. Запросы приходят в middleware, но не завершаются.

## Решение

### 1. Backend исправлен
Middleware теперь не блокирует multipart запросы - они проходят напрямую без чтения body.

### 2. Проверьте nginx на сервере

Убедитесь, что в `/etc/nginx/sites-available/default` или в конфигурации вашего сайта есть правильные настройки:

```nginx
location /api {
    proxy_pass http://89.104.67.36:55501;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection keep-alive;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_cache_bypass $http_upgrade;
    
    # Важно: сохраняем оригинальные заголовки для FormData
    proxy_set_header X-Original-Host $host;
    proxy_set_header X-Forwarded-Host $host;
    
    # Увеличиваем таймауты для больших запросов с файлами
    proxy_connect_timeout 300s;
    proxy_send_timeout 300s;
    proxy_read_timeout 300s;
    
    # Отключаем буферизацию для больших файлов
    proxy_buffering off;
    proxy_request_buffering off;
    
    # Увеличиваем размер буфера для больших запросов
    proxy_buffer_size 128k;
    proxy_buffers 4 256k;
    proxy_busy_buffers_size 256k;
}
```

### 3. Проверьте настройки в nginx.conf

В главном файле конфигурации nginx (`/etc/nginx/nginx.conf`) убедитесь, что есть:

```nginx
http {
    # Увеличиваем размеры для больших запросов
    client_max_body_size 50M;
    client_body_timeout 300s;
    client_header_timeout 60s;
    client_body_buffer_size 1M;
    
    # ... остальные настройки
}
```

### 4. Перезапустите nginx после изменений

```bash
sudo nginx -t  # Проверка конфигурации
sudo systemctl reload nginx  # Перезагрузка без остановки
# или
sudo systemctl restart nginx  # Полный перезапуск
```

### 5. Проверьте логи nginx

```bash
# Логи ошибок
sudo tail -f /var/log/nginx/error.log

# Логи доступа
sudo tail -f /var/log/nginx/access.log
```

## Диагностика

Если проблема сохраняется, проверьте:

1. **Логи backend** - должны показывать, что запрос дошел до контроллера
2. **Логи nginx** - могут показать таймауты или ошибки проксирования
3. **Сетевое соединение** - проверьте, что порт 55501 открыт и доступен

## Проверка на сервере

Выполните на сервере:

```bash
# Проверьте конфигурацию nginx
sudo nginx -t

# Проверьте, что backend слушает на правильном порту
sudo netstat -tlnp | grep 55501

# Проверьте логи Docker контейнера backend
docker logs bebochka-backend --tail 100 -f
```

