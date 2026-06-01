-- Диагностика: почему не даётся скидка −10% приглашённому.
-- Подставьте свой логин/email/телефон или код BEBO-XXXXXX.
-- mysql -u USER -p bebochka < Database/check_referral_user_orders.sql

USE bebochka;

-- === 1) Найти пользователя (раскомментируйте нужное) ===
SET @login := 'ВАШ_EMAIL_ИЛИ_USERNAME';   -- email или username
-- SET @phone := '79001234567';
-- SET @referral_code := 'BEBO-M963T5';

SELECT u.Id AS UserId, u.Username, u.Email, u.Phone, u.FullName
FROM users u
WHERE u.Email = @login
   OR u.Username = @login
LIMIT 5;
-- OR: WHERE REPLACE(REPLACE(u.Phone, '+', ''), ' ', '') LIKE CONCAT('%', @phone, '%')

-- === 2) Подставьте UserId из шага 1 ===
SET @user_id := 0;  -- <-- замените на реальный Id

-- Заказы, которые блокируют скидку (все кроме «Отменен»)
SELECT
  o.Id,
  o.OrderNumber,
  o.Status,
  o.TotalAmount,
  o.CreatedAt,
  o.ReferralId,
  o.ReferralDiscountKind,
  CASE
    WHEN o.Status = 'Отменен' THEN 'не блокирует'
    ELSE 'БЛОКИРУЕТ скидку приглашённого'
  END AS BlocksReferredDiscount
FROM orders o
WHERE o.UserId = @user_id
ORDER BY o.CreatedAt DESC;

SELECT COUNT(*) AS NonCancelledOrderCount
FROM orders o
WHERE o.UserId = @user_id AND o.Status != 'Отменен';

-- Реферальная связь «вас пригласили»
SELECT
  r.Id AS ReferralId,
  rc.Code AS InviterCode,
  r.Status,
  r.ReferredDiscountOrderId,
  r.ReferrerDiscountOrderId,
  r.CreatedAt,
  r.RegisteredAt,
  CASE
    WHEN r.ReferredDiscountOrderId IS NOT NULL THEN 'скидка приглашённого УЖЕ использована'
    WHEN r.Status = 'Pending' THEN 'ожидает регистрации — скидка недоступна'
    ELSE 'скидка −10% должна быть доступна (если нет заказов выше)'
  END AS ReferredDiscountHint
FROM referrals r
JOIN referralcodes rc ON rc.Id = r.ReferralCodeId
WHERE r.ReferredUserId = @user_id;

-- Сводка как в API
SELECT
  @user_id AS UserId,
  EXISTS(
    SELECT 1 FROM orders o
    WHERE o.UserId = @user_id AND o.Status != 'Отменен'
  ) AS HasPriorOrders,
  EXISTS(
    SELECT 1 FROM referrals r
    WHERE r.ReferredUserId = @user_id
      AND r.ReferredDiscountOrderId IS NULL
      AND r.Status != 'Pending'
  ) AS ReferredDiscountAvailable;
