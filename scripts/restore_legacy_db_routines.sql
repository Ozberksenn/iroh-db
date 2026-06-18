-- ============================================================
-- ROLLBACK: recreate legacy fn_/usp_/vw_ routines
-- Generated from live iroh_db. Run this if you need to undo
-- drop_legacy_db_routines.sql. Functions/procs first, then views.
-- ============================================================
BEGIN;

CREATE OR REPLACE PROCEDURE public.usp_create_user(IN p_name character varying, IN p_mail character varying, IN p_password text, IN p_phone character varying DEFAULT ''::character varying, IN p_lastname character varying DEFAULT ''::character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	IF p_name IS NULL 
	THEN
		RAISE EXCEPTION 'name not null';
	END IF;

	IF p_mail IS NULL OR p_password IS NULL THEN
        RAISE EXCEPTION 'mail or password not null';
    END IF;

	 INSERT INTO users (
        name,
        lastname,
        password,
        phone,
        mail,
        isactive
    )
    VALUES (
        p_name,
        p_lastname,
        p_password,
        p_phone,
        p_mail,
        true
    );
	
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_delete_child(IN p_id bigint)
 LANGUAGE plpgsql
AS $procedure$
      BEGIN
          -- Aktif oturum var mı kontrol et
          IF EXISTS (
              SELECT 1 FROM bookings WHERE child_id = p_id AND status IN ('Active', 'Paused')
          ) THEN
              RAISE EXCEPTION 'Bu çocuğun şu an içeride aktif bir oturumu var. Oturum kapatılmadan silinemez!';
          END IF;

          -- Soft delete yap
          UPDATE children
          SET is_deleted = true, updated_at = now()
          WHERE id = p_id;

          IF NOT FOUND THEN
              RAISE EXCEPTION 'Çocuk bulunamadı!';
          END IF;
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_delete_customer(IN p_id bigint)
 LANGUAGE plpgsql
AS $procedure$
      BEGIN
          IF p_id = 999999 THEN
              RAISE EXCEPTION 'Sistem Misafiri kaydı silinemez!';
          END IF;
          IF EXISTS (SELECT 1 FROM bookings b JOIN children ch ON b.child_id = ch.id WHERE ch.parent_id = p_id AND b.status IN ('Active', 'Paused')) THEN
              RAISE EXCEPTION 'Bu ebeveynin bir çocuğu şu an içeride aktif oturumda. Oturum kapanmadan silinemez!';
          END IF;
          UPDATE children SET is_deleted = TRUE, updated_at = now() WHERE parent_id = p_id;
          UPDATE customers SET isdeleted = TRUE, updatedat = now() WHERE id = p_id;
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_delete_package(IN p_id bigint)
 LANGUAGE plpgsql
AS $procedure$
      BEGIN
          UPDATE packages
          SET is_deleted = true, 
              updated_at = now()
          WHERE id = p_id;

          IF NOT FOUND THEN
              RAISE EXCEPTION 'Paket bulunamadı!';
          END IF;
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_delete_purchase(IN p_id bigint)
 LANGUAGE plpgsql
AS $procedure$
      BEGIN
          -- Silme işlemini tamamen engelliyoruz.
          -- Finansal kayıtlar silinemez, sadece düzeltme girilebilir.
          RAISE EXCEPTION 'Güvenlik ve denetim gereği satın alım kayıtları silinemez! Yanlış bir işlem yaptıysanız lütfen ek ödeme (payment) ile bakiyeyi dengeleyin veya yöneticiye başvurun.';
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_delete_table(IN p_id bigint)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	 IF p_id IS NULL THEN
        RAISE EXCEPTION 'id is required!';
    END IF;

	-- aktif booking kontrolü
	IF EXISTS (
	    SELECT 1
	    FROM bookings b
	    WHERE b.tableid = p_id
	      AND b.status IN ('Active', 'Paused')
	) THEN
        RAISE EXCEPTION 'Bu masaya ait aktif rezervasyon var. Silinemez!';
    END IF;

    UPDATE tables
	SET isdeleted = TRUE
    WHERE id = p_id;
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_insert_booking_log(IN p_bookingid bigint, IN p_time timestamp with time zone, IN p_type character varying, IN p_userid bigint)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	IF p_bookingid IS NULL THEN
        RAISE EXCEPTION 'booking id is required!';
    END IF;

	IF p_time IS NULL THEN
        RAISE EXCEPTION 'time is required!';
    END IF;

	IF p_type IS NULL THEN
        RAISE EXCEPTION 'type is required!';
    END IF;

	IF p_userid IS NULL THEN
        RAISE EXCEPTION 'userId is required!';
    END IF;

	INSERT INTO bookinglogs(bookingId,time,type,userId)
	VALUES(p_bookingid,p_time,p_type,p_userid);	
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_insert_customer(IN p_name character varying, IN p_lastname character varying, IN p_phone character varying, IN p_mail character varying)
 LANGUAGE plpgsql
AS $procedure$
      BEGIN
        INSERT INTO customers(name, lastname, phone, mail, createdat)
        VALUES(p_name, p_lastname, p_phone, p_mail, now());
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_insert_customer(IN p_name character varying, IN p_lastname character varying, IN p_parentname character varying, IN p_parentlastname character varying, IN p_phone character varying, IN p_mail character varying, IN p_parentphone character varying, IN p_parentmail character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	IF p_name IS NULL THEN
		RAISE EXCEPTION 'Name is required';
	END IF;

	IF p_phone IS NULL THEN
		RAISE EXCEPTION 'Phone is required';
	END IF;

	INSERT INTO customers(name,lastName,parentName,parentLastName,phone,mail,parentPhone,parentMail,createdAt)
	VALUES(p_name,p_lastName,p_parentName,p_parentLastName,p_phone,p_mail,p_parentPhone,p_parentMail,now());
	
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_insert_purchase(IN p_hours numeric, IN p_price numeric, IN p_customerid bigint, IN p_startdate timestamp with time zone, IN p_enddate timestamp with time zone)
 LANGUAGE plpgsql
AS $procedure$
      BEGIN
          IF p_customerid = 999999 THEN
              RAISE EXCEPTION 'Sistem Misafiri kaydına paket tanımlanamaz!';
          END IF;
          INSERT INTO purchases (hours, price, customerid, startdate, enddate, createdat)
          VALUES (p_hours, p_price, p_customerid, p_startdate, p_enddate, CURRENT_TIMESTAMP);
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_insert_purchase_payment(IN p_purchaseid bigint, IN p_hours numeric, IN p_price numeric)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    IF p_purchaseId IS NULL THEN
        RAISE EXCEPTION 'purchaseId is required!';
    END IF;

    IF p_hours IS NULL THEN
        RAISE EXCEPTION 'hours is required!';
    END IF;

    IF p_price IS NULL THEN
        RAISE EXCEPTION 'price is required!';
    END IF;

    INSERT INTO purchasepayments(purchaseid, hours, price)
    VALUES (p_purchaseId, p_hours, p_price);
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_insert_table(IN p_name character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    IF p_name IS NULL THEN
        RAISE EXCEPTION 'Name is required !';
    END IF;

    INSERT INTO tables (name)
    VALUES (p_name);
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_booking(IN p_id bigint, IN p_tableid bigint, IN p_childid bigint, IN p_starttime timestamp with time zone, IN p_endtime timestamp with time zone, IN p_status character varying, IN p_price numeric, IN p_note text, IN p_purchaseid bigint, IN p_subscriptionstarttime timestamp with time zone, IN p_subscriptionendtime timestamp with time zone)
 LANGUAGE plpgsql
AS $procedure$
      DECLARE
        v_bookingId BIGINT;
      BEGIN
        UPDATE bookings
        SET tableid = p_tableid, 
            child_id = p_childid, 
            starttime = p_starttime, 
            endtime = p_endtime,
            status = p_status, 
            price = p_price, 
            note = p_note, 
            subscriptionstarttime = p_subscriptionstarttime, 
            subscriptionendtime = p_subscriptionendtime
        WHERE id = p_id
        RETURNING id INTO v_bookingId;

        -- Eğer bir paket (Purchase) kullanılıyorsa, onu da bağla
        IF p_purchaseid IS NOT NULL THEN
          INSERT INTO purchasebookings(bookingid, purchaseid)
          SELECT v_bookingId, p_purchaseid
          WHERE NOT EXISTS (
            SELECT 1 FROM purchasebookings pb WHERE pb.bookingid = v_bookingId AND pb.purchaseid = p_purchaseid
          );
        END IF;
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_bookinglog(IN p_id bigint, IN p_bookingid bigint, IN p_time timestamp with time zone, IN p_type character varying, IN p_userid bigint)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	IF p_id IS NULL THEN
        RAISE EXCEPTION 'id is required!';
    END IF;
	
	IF p_bookingId IS NULL THEN
        RAISE EXCEPTION 'bookingId is required!';
    END IF;

	IF p_bookingId IS NULL THEN
        RAISE EXCEPTION 'bookingId is required!';
    END IF;

	IF p_time IS NULL THEN
        RAISE EXCEPTION 'time is required!';
    END IF;

	IF p_type IS NULL THEN
        RAISE EXCEPTION 'type is required!';
    END IF;

	 IF p_userId IS NULL THEN
        RAISE EXCEPTION 'userId is required!';
    END IF;

	UPDATE bookinglogs
	SET bookingId = p_bookingId,time=p_time,type=p_type,userId=p_userId
	WHERE id = p_id;

	IF NOT FOUND THEN
        RAISE EXCEPTION 'booking log not found!';
    END IF;
	
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_child(IN p_id bigint, IN p_name character varying, IN p_birth_date date)
 LANGUAGE plpgsql
AS $procedure$
      BEGIN
          UPDATE children
          SET name = p_name,
              birth_date = p_birth_date,
              updated_at = now()
          WHERE id = p_id AND is_deleted = false;

          IF NOT FOUND THEN
              RAISE EXCEPTION 'Çocuk bulunamadı veya silinmiş!';
          END IF;
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_company(IN p_id bigint, IN p_name character varying, IN p_firsthourprice numeric, IN p_additionalhalfhourprice numeric)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	IF p_id IS NULL THEN
        RAISE EXCEPTION 'id is required!';
    END IF;

	UPDATE company
	SET name=p_name,firstHourPrice=p_firstHourPrice,additionalHalfHourPrice=p_additionalHalfHourPrice
	WHERE id=p_id;

END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_customer(IN p_id bigint, IN p_name character varying, IN p_lastname character varying, IN p_phone character varying, IN p_mail character varying)
 LANGUAGE plpgsql
AS $procedure$
      BEGIN
          IF p_id = 999999 THEN
              RAISE EXCEPTION 'Sistem Misafiri kaydı değiştirilemez!';
          END IF;
          UPDATE customers SET name=p_name, lastname=p_lastname, phone=p_phone, mail=p_mail, updatedat = now() WHERE id=p_id;
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_customer(IN p_id bigint, IN p_name character varying, IN p_lastname character varying, IN p_parentname character varying, IN p_parentlastname character varying, IN p_phone character varying, IN p_mail character varying, IN p_parentphone character varying, IN p_parentmail character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	 IF p_id IS NULL THEN
        RAISE EXCEPTION 'id is required!';
    END IF;
	IF p_name IS NULL THEN
        RAISE EXCEPTION 'name is required!';
    END IF;
	IF p_phone IS NULL THEN
        RAISE EXCEPTION 'phone is required!';
    END IF;

	UPDATE customers
	SET name=p_name,lastName=p_lastName,parentName=p_parentName,parentLastName=p_parentLastName,
			 updatedat = now(),phone=p_phone,mail=p_mail,parentPhone=p_parentPhone,parentMail=p_parentMail
	WHERE id=p_id;

	IF NOT FOUND THEN
        RAISE EXCEPTION 'customer not found!';
    END IF;
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_package(IN p_id bigint, IN p_name character varying, IN p_hours numeric, IN p_price numeric, IN p_validity_days integer)
 LANGUAGE plpgsql
AS $procedure$
      BEGIN
          UPDATE packages
          SET name = p_name,
              hours = p_hours,
              price = p_price,
              validity_days = p_validity_days,
              updated_at = now()
          WHERE id = p_id AND is_deleted = false;

          IF NOT FOUND THEN
              RAISE EXCEPTION 'Paket bulunamadı veya silinmiş!';
          END IF;
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_password(IN p_mail character varying, IN p_newpassword text)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    IF p_mail IS NULL OR p_newpassword IS NULL THEN
        RAISE EXCEPTION 'mail or password is required!';
    END IF;

    UPDATE users
    SET password = p_newpassword
    WHERE mail = p_mail;

    -- hi?? kullan??c?? bulunamad??ysa
    IF NOT FOUND THEN
        RAISE EXCEPTION 'user not found!';
    END IF;
END;
$procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_purchase(IN p_id bigint, IN p_hours numeric, IN p_price numeric, IN p_customerid bigint, IN p_startdate timestamp with time zone, IN p_enddate timestamp with time zone)
 LANGUAGE plpgsql
AS $procedure$
      DECLARE
          v_old_hours NUMERIC;
          v_has_usage BOOLEAN;
          v_has_payments BOOLEAN;
      BEGIN
          -- 1. Mevcut saati ve kullanım durumlarını kontrol et
          SELECT hours INTO v_old_hours FROM purchases WHERE id = p_id;
          
          SELECT EXISTS(SELECT 1 FROM purchasebookings WHERE purchaseid = p_id) INTO v_has_usage;
          SELECT EXISTS(SELECT 1 FROM purchasepayments WHERE purchaseid = p_id) INTO v_has_payments;

          -- 2. Eğer saat değiştirilmek isteniyorsa ve kullanım/ödeme varsa ENGELLE
          IF v_old_hours != p_hours AND (v_has_usage OR v_has_payments) THEN
              RAISE EXCEPTION 'Bu paket üzerinde kullanım veya ek ödeme mevcut. Ana saat bilgisi değiştirilemez! Lütfen düzeltme için ek ödeme (payment) ekleyin.';
          END IF;

          -- 3. Güncelleme işlemini yap
          UPDATE purchases
          SET hours = p_hours,
              price = p_price,
              updatedat = now(),
              customerid = p_customerid,
              startdate = p_startdate,
              enddate = p_enddate
          WHERE id = p_id;

          IF NOT FOUND THEN
              RAISE EXCEPTION 'Paket bulunamadı!';
          END IF;
      END;
      $procedure$
;

CREATE OR REPLACE PROCEDURE public.usp_update_table(IN p_id bigint, IN p_name character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
	IF p_id IS NULL THEN
		RAISE EXCEPTION 'id is required !';
	END IF;

	UPDATE tables
	SET name=p_name
	WHERE id=p_id;
END;
$procedure$
;

CREATE OR REPLACE FUNCTION public.fn_get_children_by_parent_id(p_parent_id bigint)
 RETURNS TABLE(id bigint, parent_id bigint, name character varying, birth_date date, created_at timestamp with time zone)
 LANGUAGE plpgsql
AS $function$
      BEGIN
          RETURN QUERY
          SELECT c.id, c.parent_id, c.name, c.birth_date, c.created_at
          FROM children c
          WHERE c.parent_id = p_parent_id AND c.is_deleted = false
          ORDER BY c.created_at DESC;
      END;
      $function$
;

CREATE OR REPLACE FUNCTION public.fn_get_customer_by_id(p_id bigint)
 RETURNS SETOF customers
 LANGUAGE plpgsql
AS $function$
    BEGIN
        RETURN QUERY
        SELECT *
        FROM customers
        WHERE id = p_id
          AND isdeleted = false;
    END;
    $function$
;

CREATE OR REPLACE FUNCTION public.fn_get_customers(p_status character varying DEFAULT NULL::character varying, p_page integer DEFAULT 1, p_size integer DEFAULT 50, p_name text DEFAULT NULL::text, p_mail text DEFAULT NULL::text)
 RETURNS TABLE(id bigint, name character varying, "lastName" character varying, phone character varying, mail character varying, status text, "totalCount" double precision)
 LANGUAGE plpgsql
AS $function$ BEGIN RETURN QUERY WITH cte AS ( SELECT c.id, c.name, c.lastname, c.phone, c.mail, ( SELECT CASE WHEN EXISTS (SELECT 1 FROM purchases p2 WHERE p2.customerid = c.id AND CURRENT_TIMESTAMP BETWEEN p2.startdate AND p2.enddate) THEN 'ActiveSubscriber' WHEN EXISTS (SELECT 1 FROM purchases p2 WHERE p2.customerid = c.id) THEN 'Subscriber' ELSE 'Customer' END ) AS customer_status, COALESCE((SELECT string_agg(ch.name, ' ') FROM children ch WHERE ch.parent_id = c.id), '') AS children_names FROM customers c WHERE c.isdeleted = false AND c.id != 999999 ) SELECT cte.id, cte.name, cte.lastname, cte.phone, cte.mail, cte.customer_status, COUNT(*) OVER()::double precision FROM cte WHERE (p_name IS NULL OR (cte.name || ' ' || COALESCE(cte.lastname, '') || ' ' || COALESCE(cte.phone, '') || ' ' || COALESCE(cte.mail, '') || ' ' || cte.children_names) ILIKE '%' || p_name || '%') AND (p_status IS NULL OR cte.customer_status = p_status) ORDER BY cte.id OFFSET CASE WHEN p_page = -1 THEN 0 ELSE (p_page - 1) * p_size END LIMIT CASE WHEN p_page = -1 THEN NULL ELSE p_size END; END; $function$
;

CREATE OR REPLACE FUNCTION public.fn_get_dashboard_busy_hours(p_start_date timestamp with time zone, p_end_date timestamp with time zone)
 RETURNS TABLE(hour text, count bigint)
 LANGUAGE plpgsql
AS $function$
      BEGIN
          RETURN QUERY
          SELECT
              LPAD(
                EXTRACT(HOUR FROM starttime AT TIME ZONE 'Europe/Istanbul')::text,
                2, '0'
              ) || ':00' AS hour,
              COUNT(*)::bigint AS count
          FROM bookings
          WHERE starttime >= p_start_date
            AND starttime <= p_end_date
          GROUP BY EXTRACT(HOUR FROM starttime AT TIME ZONE 'Europe/Istanbul')
          ORDER BY EXTRACT(HOUR FROM starttime AT TIME ZONE 'Europe/Istanbul') ASC;
      END;
      $function$
;

CREATE OR REPLACE FUNCTION public.fn_get_dashboard_daily_list(p_start_date timestamp with time zone, p_end_date timestamp with time zone)
 RETURNS TABLE(booking_id bigint, name character varying, lastname character varying, parentname character varying, check_in timestamp with time zone, check_out timestamp with time zone, status character varying, price numeric, is_subscription boolean, parent_id bigint)
 LANGUAGE plpgsql
AS $function$ BEGIN RETURN QUERY SELECT b.id as booking_id, COALESCE(ch.name, 'Bilinmeyen'), ''::varchar as lastname, (COALESCE(c.name, 'Misafir') || ' ' || COALESCE(c.lastname, ''))::varchar as parentname, b.starttime as check_in, b.endtime as check_out, b.status, b.price, CASE WHEN pb.bookingid IS NOT NULL THEN true ELSE false END as is_subscription, c.id as parent_id FROM bookings b LEFT JOIN children ch ON ch.id = b.child_id LEFT JOIN customers c ON ch.parent_id = c.id LEFT JOIN purchasebookings pb ON b.id = pb.bookingid WHERE b.starttime >= p_start_date AND b.starttime <= p_end_date AND (c.id IS NULL OR c.isdeleted = false) ORDER BY b.starttime DESC LIMIT 10; END; $function$
;

CREATE OR REPLACE FUNCTION public.fn_get_dashboard_overview(p_start_date timestamp with time zone, p_end_date timestamp with time zone)
 RETURNS TABLE(total_children bigint, active_currently bigint, booking_revenue numeric, canceled_count bigint, avg_duration_minutes numeric, subscription_sessions bigint)
 LANGUAGE plpgsql
AS $function$
      BEGIN
          RETURN QUERY
          SELECT
              COUNT(DISTINCT b.id)::bigint as total_children,
              COUNT(DISTINCT b.id) FILTER (WHERE b.status IN ('Active', 'active'))::bigint as active_currently,
              COALESCE(SUM(b.price), 0)::numeric as booking_revenue,
              COUNT(DISTINCT b.id) FILTER (WHERE b.status IN ('Canceled', 'canceled'))::bigint as canceled_count,
              COALESCE(AVG(EXTRACT(EPOCH FROM (b.endtime - b.starttime))/60) FILTER (WHERE b.endtime IS NOT NULL AND b.status != 'Canceled'), 0)::numeric as avg_duration_minutes,
              COUNT(DISTINCT pb.bookingid)::bigint as subscription_sessions
          FROM bookings b
          LEFT JOIN children ch ON b.child_id = ch.id
          LEFT JOIN customers c ON ch.parent_id = c.id
          LEFT JOIN purchasebookings pb ON b.id = pb.bookingid
          WHERE b.starttime >= p_start_date AND b.starttime <= p_end_date
            AND (c.id IS NULL OR c.isdeleted = false); -- Ebeveyn varsa silinmemiş olmalı, yoksa (isimsizse) yine de say
      END;
      $function$
;

CREATE OR REPLACE FUNCTION public.fn_get_dashboard_purchases(p_start_date timestamp with time zone, p_end_date timestamp with time zone)
 RETURNS TABLE(purchase_count bigint, purchase_revenue numeric)
 LANGUAGE plpgsql
AS $function$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(*)::bigint as purchase_count,
        COALESCE(SUM(price), 0)::numeric as purchase_revenue
    FROM purchases
    WHERE createdat >= p_start_date AND createdat <= p_end_date;
END;
$function$
;

CREATE OR REPLACE FUNCTION public.fn_get_dashboard_revenue_chart(p_start_date timestamp with time zone, p_end_date timestamp with time zone)
 RETURNS TABLE(date text, "bookingRevenue" numeric, "purchaseRevenue" numeric)
 LANGUAGE plpgsql
AS $function$
      BEGIN
          RETURN QUERY
          WITH date_series AS (
              SELECT generate_series(
                  DATE(p_start_date AT TIME ZONE 'Europe/Istanbul'),
                  DATE(p_end_date   AT TIME ZONE 'Europe/Istanbul'),
                  '1 day'::interval
              )::date AS ddate
          ),
          booking_rev AS (
              SELECT
                  DATE(starttime AT TIME ZONE 'Europe/Istanbul') AS bdate,
                  COALESCE(SUM(price), 0) AS b_rev
              FROM bookings
              WHERE starttime >= p_start_date
                AND starttime <= p_end_date
              GROUP BY DATE(starttime AT TIME ZONE 'Europe/Istanbul')
          ),
          purchase_rev AS (
              SELECT
                  DATE(createdat AT TIME ZONE 'Europe/Istanbul') AS pdate,
                  COALESCE(SUM(price), 0) AS p_rev
              FROM purchases
              WHERE createdat >= p_start_date
                AND createdat <= p_end_date
              GROUP BY DATE(createdat AT TIME ZONE 'Europe/Istanbul')
          )
          SELECT
              TO_CHAR(ds.ddate, 'DD.MM')             AS date,
              COALESCE(b.b_rev, 0)::numeric           AS "bookingRevenue",
              COALESCE(p.p_rev, 0)::numeric           AS "purchaseRevenue"
          FROM date_series ds
          LEFT JOIN booking_rev  b ON ds.ddate = b.bdate
          LEFT JOIN purchase_rev p ON ds.ddate = p.pdate
          ORDER BY ds.ddate;
      END;
      $function$
;

CREATE OR REPLACE FUNCTION public.fn_get_dashboard_top_customers(p_start_date timestamp with time zone, p_end_date timestamp with time zone)
 RETURNS TABLE(name character varying, lastname character varying, visit_count bigint, purchase_count bigint, booking_spent numeric, purchase_spent numeric, total_spent numeric, customer_id bigint)
 LANGUAGE plpgsql
AS $function$ BEGIN RETURN QUERY WITH customer_booking_stats AS ( SELECT ch.parent_id as customerid, COUNT(b.id) as visit_count, COALESCE(SUM(b.price), 0) as booking_spent FROM bookings b JOIN children ch ON b.child_id = ch.id WHERE b.starttime >= p_start_date AND b.starttime <= p_end_date AND ch.is_deleted = false GROUP BY ch.parent_id ), customer_purchase_stats AS ( SELECT p.customerid, COALESCE(SUM(p.price), 0) as purchase_spent, COUNT(p.id) as purchase_count FROM purchases p WHERE p.createdat >= p_start_date AND p.createdat <= p_end_date GROUP BY p.customerid ) SELECT c.name, c.lastname, COALESCE(b.visit_count, 0)::bigint as visit_count, COALESCE(p.purchase_count, 0)::bigint as purchase_count, COALESCE(b.booking_spent, 0)::numeric as booking_spent, COALESCE(p.purchase_spent, 0)::numeric as purchase_spent, (COALESCE(b.booking_spent, 0) + COALESCE(p.purchase_spent, 0))::numeric as total_spent, c.id as customer_id FROM customers c LEFT JOIN customer_booking_stats b ON c.id = b.customerid LEFT JOIN customer_purchase_stats p ON c.id = p.customerid WHERE c.isdeleted = false AND (COALESCE(b.visit_count, 0) > 0 OR COALESCE(p.purchase_count, 0) > 0) ORDER BY total_spent DESC LIMIT 10; END; $function$
;

CREATE OR REPLACE FUNCTION public.fn_get_purchase_by_customer_id(p_customerid bigint)
 RETURNS TABLE(id bigint, "customerId" bigint, "startDate" timestamp with time zone, "endDate" timestamp with time zone, hours double precision, price double precision, "usedHours" double precision, payments json)
 LANGUAGE plpgsql
AS $function$
BEGIN
    IF p_customerid IS NULL THEN
        RAISE EXCEPTION 'customerId is required!';
    END IF;

    RETURN QUERY
    SELECT
        p.id,
        p.customerid,
        p.startdate,
        p.enddate,
        p.hours::double precision,
        p.price::double precision,
        -- Hesaplamay?? LEFT JOIN ile buraya ald??k:
        COALESCE(used_calc.total_minutes, 0)::double precision AS "usedHours",
        (
         SELECT COALESCE(json_agg(pp), '[]'::json)
         FROM purchasepayments pp
         WHERE pp.purchaseid = p.id
        ) AS payments
    FROM purchases p
    -- Used Hours hesaplamas?? i??in toplu join
    LEFT JOIN (
        SELECT 
            pb.purchaseid,
            SUM(EXTRACT(EPOCH FROM (b.subscriptionEndTime - b.subscriptionStartTime)) / 60) as total_minutes
        FROM purchasebookings pb
        INNER JOIN bookings b ON pb.bookingid = b.id
        GROUP BY pb.purchaseid
    ) used_calc ON used_calc.purchaseid = p.id
    WHERE p.customerid = p_customerid;
END;
$function$
;

CREATE OR REPLACE FUNCTION public.fn_get_used_hours(p_purchaseid bigint)
 RETURNS TABLE(usedhours numeric)
 LANGUAGE sql
AS $function$
          SELECT
          COALESCE(
              SUM(EXTRACT(EPOCH FROM (b.subscriptionEndTime - b.subscriptionStartTime)) / 60),
              0
          ) AS usedhours
          FROM purchasebookings pb
          INNER JOIN bookings b ON pb.bookingid = b.id
          WHERE pb.purchaseid = p_purchaseid;
      $function$
;

CREATE OR REPLACE FUNCTION public.fn_insert_booking(p_tableid bigint, p_starttime timestamp with time zone, p_endtime timestamp with time zone, p_status character varying, p_price numeric, p_childid bigint, p_note text)
 RETURNS TABLE(id bigint)
 LANGUAGE plpgsql
AS $function$
      BEGIN
          -- Fonksiyonun dönen bir set üretmesi için RETURN QUERY şarttır
          RETURN QUERY
          INSERT INTO bookings (tableid, starttime, endtime, status, price, child_id, note)
          VALUES (p_tableid, p_starttime, p_endtime, p_status, p_price, p_childid, p_note)
          RETURNING bookings.id;
      END;
      $function$
;

CREATE OR REPLACE FUNCTION public.fn_insert_child(p_parent_id bigint, p_name character varying, p_birth_date date DEFAULT NULL::date)
 RETURNS TABLE(id bigint, parent_id bigint, name character varying, birth_date date, created_at timestamp with time zone)
 LANGUAGE plpgsql
AS $function$
      BEGIN
          IF p_parent_id = 999999 THEN
              RAISE EXCEPTION 'Sistem Misafiri kaydına ek çocuk eklenemez!';
          END IF;
          RETURN QUERY
          INSERT INTO children (parent_id, name, birth_date, created_at, updated_at, is_deleted)
          VALUES (p_parent_id, p_name, p_birth_date, now(), now(), false)
          RETURNING children.id, children.parent_id, children.name, children.birth_date, children.created_at;
      END;
      $function$
;

CREATE OR REPLACE FUNCTION public.fn_insert_package(p_name character varying, p_hours numeric, p_price numeric, p_validity_days integer DEFAULT NULL::integer)
 RETURNS TABLE(id bigint, name character varying, hours numeric, price numeric, validity_days integer, created_at timestamp with time zone)
 LANGUAGE plpgsql
AS $function$
      BEGIN
          RETURN QUERY
          INSERT INTO packages (name, hours, price, validity_days, created_at, updated_at, is_deleted)
          VALUES (p_name, p_hours, p_price, p_validity_days, now(), now(), false)
          RETURNING packages.id, packages.name, packages.hours, packages.price, packages.validity_days, packages.created_at;
      END;
      $function$
;

CREATE OR REPLACE FUNCTION public.fn_login(p_mail character varying)
 RETURNS TABLE(id bigint, mail text, password text)
 LANGUAGE plpgsql
AS $function$
BEGIN
	 IF p_mail IS NULL THEN
        RAISE EXCEPTION 'mail must not be null';
    END IF;

	RETURN QUERY
	  SELECT
        u.id,
        u.mail::text,
        u.password::text
    FROM users u
    WHERE u.mail = p_mail;
END;
$function$
;

CREATE OR REPLACE FUNCTION public.fn_search_unified(p_search text)
 RETURNS TABLE(child_id bigint, child_name character varying, parent_id bigint, parent_name character varying, parent_phone character varying, status text, remaining_hours numeric, is_active boolean, current_table_name character varying)
 LANGUAGE plpgsql
AS $function$
      BEGIN
          RETURN QUERY
          WITH parent_best_package AS (
              SELECT DISTINCT ON (p.customerid)
                  p.id as purchase_id, p.customerid,
                  ((p.hours + COALESCE((SELECT SUM(pp.hours) FROM purchasepayments pp WHERE pp.purchaseid = p.id), 0)) * 60) - 
                  COALESCE((SELECT usedhours FROM fn_get_used_hours(p.id)), 0) as rem_minutes,
                  (CURRENT_TIMESTAMP BETWEEN p.startdate AND p.enddate) as is_date_valid
              FROM purchases p
              ORDER BY 
                  p.customerid, 
                  (CURRENT_TIMESTAMP BETWEEN p.startdate AND p.enddate) DESC,
                  -- DÜZELTME: rem_minutes yerine açık hesaplama yazdık
                  (((p.hours + COALESCE((SELECT SUM(pp.hours) FROM purchasepayments pp WHERE pp.purchaseid = p.id), 0)) * 60) - COALESCE((SELECT usedhours FROM fn_get_used_hours(p.id)), 0) > 0) DESC,
                  p.enddate DESC
          )
          SELECT 
              ch.id as child_id, ch.name as child_name, c.id as parent_id, (c.name || ' ' || c.lastname)::varchar as parent_name, c.phone as parent_phone,
              CASE 
                WHEN pbp.is_date_valid AND pbp.rem_minutes > 0 THEN 'ActiveSubscriber'
                WHEN pbp.is_date_valid THEN 'OverageSubscriber'
                WHEN EXISTS (SELECT 1 FROM purchases p3 WHERE p3.customerid = c.id AND p3.startdate > CURRENT_TIMESTAMP) THEN 'UpcomingSubscriber'
                ELSE 'Customer'
              END as status,
              (COALESCE(pbp.rem_minutes, 0) / 60.0)::numeric as remaining_hours,
              EXISTS(SELECT 1 FROM bookings b WHERE b.child_id = ch.id AND b.status IN ('Active', 'Paused')) as is_active,
              (SELECT t.name FROM bookings b JOIN tables t ON b.tableid = t.id WHERE b.child_id = ch.id AND b.status IN ('Active', 'Paused') LIMIT 1)::varchar as current_table_name
          FROM customers c
          LEFT JOIN parent_best_package pbp ON c.id = pbp.customerid
          LEFT JOIN children ch ON ch.parent_id = c.id AND ch.is_deleted = false
          WHERE c.isdeleted = false AND c.id != 999999
            AND (c.name ILIKE '%' || p_search || '%' OR c.lastname ILIKE '%' || p_search || '%' OR c.phone ILIKE '%' || p_search || '%' OR ch.name ILIKE '%' || p_search || '%')
          ORDER BY (CASE WHEN pbp.is_date_valid AND pbp.rem_minutes > 0 THEN 0 WHEN pbp.is_date_valid THEN 1 ELSE 2 END), c.name ASC
          LIMIT 50;
      END;
      $function$
;

CREATE OR REPLACE FUNCTION public.usp_get_bookings(p_page integer DEFAULT 1, p_size integer DEFAULT 20, p_status text[] DEFAULT NULL::text[], p_name text DEFAULT NULL::text, p_mail text DEFAULT NULL::text, p_customerid bigint DEFAULT NULL::bigint, p_childid bigint DEFAULT NULL::bigint, p_starttime timestamp with time zone DEFAULT NULL::timestamp with time zone, p_endtime timestamp with time zone DEFAULT NULL::timestamp with time zone, p_tableid bigint DEFAULT NULL::bigint)
 RETURNS TABLE(id bigint, "table" jsonb, customer jsonb, price double precision, "startTime" timestamp with time zone, "endTime" timestamp with time zone, "subscriptionStartTime" timestamp with time zone, "subscriptionEndTime" timestamp with time zone, status text, note text, "totalCount" double precision)
 LANGUAGE plpgsql
AS $function$ BEGIN RETURN QUERY WITH bookings_cte AS ( SELECT b.id AS booking_id, jsonb_build_object('id', t.id::text, 'name', t.name) AS table_json, jsonb_build_object('childId', ch.id::text, 'name', ch.name, 'parentId', p.id::text, 'parentName', p.name, 'parentLastName', p.lastname, 'phone', p.phone ) AS customer_json, b.price::double precision AS price_val, b.starttime AS start_time_val, b.endtime AS end_time_val, b.subscriptionstarttime AS sub_start_val, b.subscriptionendtime AS sub_end_val, b.status::text AS status_val, b.note::text AS note_val FROM bookings b LEFT JOIN children ch ON ch.id = b.child_id LEFT JOIN customers p ON p.id = ch.parent_id LEFT JOIN tables t ON t.id = b.tableid WHERE (p_name IS NULL OR (ch.name || ' ' || p.name || ' ' || COALESCE(p.lastname, '') || ' ' || COALESCE(p.phone, '') || ' ' || COALESCE(t.name, '') || ' ' || COALESCE(b.note, '')) ILIKE '%' || p_name || '%') AND (p_status IS NULL OR b.status = ANY(p_status)) AND (p_customerid IS NULL OR p.id = p_customerid) AND (p_childid IS NULL OR ch.id = p_childid) AND (p_starttime IS NULL OR b.starttime >= p_starttime) AND (p_endtime IS NULL OR b.starttime <= p_endtime) AND (p_tableid IS NULL OR b.tableid = p_tableid) ) SELECT booking_id, table_json, customer_json, price_val, start_time_val, end_time_val, sub_start_val, sub_end_val, status_val, note_val, COUNT(*) OVER()::double precision FROM bookings_cte ORDER BY booking_id DESC OFFSET CASE WHEN p_page = -1 THEN 0 ELSE (p_page - 1) * p_size END LIMIT CASE WHEN p_page = -1 THEN NULL ELSE p_size END; END; $function$
;

CREATE OR REPLACE FUNCTION public.usp_get_bookings(p_page integer DEFAULT 1, p_size integer DEFAULT 20, p_status text[] DEFAULT NULL::text[], p_name text DEFAULT NULL::text, p_mail text DEFAULT NULL::text, p_customerid bigint DEFAULT NULL::bigint, p_childid bigint DEFAULT NULL::bigint, p_starttime timestamp with time zone DEFAULT NULL::timestamp with time zone, p_endtime timestamp with time zone DEFAULT NULL::timestamp with time zone)
 RETURNS TABLE(id bigint, "table" jsonb, customer jsonb, price double precision, "startTime" timestamp with time zone, "endTime" timestamp with time zone, "subscriptionStartTime" timestamp with time zone, "subscriptionEndTime" timestamp with time zone, status text, note text, "totalCount" double precision)
 LANGUAGE plpgsql
AS $function$ BEGIN RETURN QUERY WITH bookings_cte AS ( SELECT b.id AS booking_id, jsonb_build_object('id', t.id::text, 'name', t.name) AS table_json, jsonb_build_object('childId', ch.id::text, 'name', ch.name, 'parentId', p.id::text, 'parentName', p.name, 'parentLastName', p.lastname, 'phone', p.phone ) AS customer_json, b.price::double precision AS price_val, b.starttime AS start_time_val, b.endtime AS end_time_val, b.subscriptionstarttime AS sub_start_val, b.subscriptionendtime AS sub_end_val, b.status::text AS status_val, b.note::text AS note_val FROM bookings b LEFT JOIN children ch ON ch.id = b.child_id LEFT JOIN customers p ON p.id = ch.parent_id LEFT JOIN tables t ON t.id = b.tableid WHERE (p_name IS NULL OR (ch.name || ' ' || p.name || ' ' || COALESCE(p.lastname, '') || ' ' || COALESCE(p.phone, '')) ILIKE '%' || p_name || '%') AND (p_status IS NULL OR b.status = ANY(p_status)) AND (p_customerid IS NULL OR p.id = p_customerid) AND (p_childid IS NULL OR ch.id = p_childid) AND (p_starttime IS NULL OR b.starttime >= p_starttime) AND (p_endtime IS NULL OR b.starttime <= p_endtime) ) SELECT booking_id, table_json, customer_json, price_val, start_time_val, end_time_val, sub_start_val, sub_end_val, status_val, note_val, COUNT(*) OVER()::double precision FROM bookings_cte ORDER BY booking_id DESC OFFSET CASE WHEN p_page = -1 THEN 0 ELSE (p_page - 1) * p_size END LIMIT CASE WHEN p_page = -1 THEN NULL ELSE p_size END; END; $function$
;

CREATE OR REPLACE FUNCTION public.usp_get_purchase_bookings_by_id(p_purchaseid bigint)
 RETURNS TABLE(id bigint, booking_id bigint, purchase_id bigint, booking jsonb)
 LANGUAGE plpgsql
AS $function$
BEGIN
	IF p_purchaseId IS NULL THEN
		RAISE EXCEPTION 'purchaseId is required !';
	END IF;

	RETURN QUERY
	SELECT 
		pb.id,
		pb.bookingId,
		pb.purchaseId,
		jsonb_build_object(
			'table',
				jsonb_build_object(
					'id',t.id,
					'name',t.name
				),
			'tableId',b.tableId,
			'customerId',b.customerId,
			'startTime',b.startTime,
			'endTime',b.endTime,
			'status',b.status,
			'note',b.note
		) AS booking
	 FROM purchaseBookings pb
	 INNER JOIN purchases p ON p.id = pb.purchaseId
	 INNER JOIN bookings b ON b.id = pb.bookingid
     INNER JOIN tables t ON t.id = b.tableid
     WHERE pb.purchaseid = p_purchaseid;

END;
$function$
;


-- Views (depend on the functions above)
CREATE OR REPLACE VIEW public.vw_activebookings AS  WITH parent_best_package AS (
         SELECT DISTINCT ON (p.customerid) p.id AS purchase_id,
            p.customerid,
            (p.hours + COALESCE(( SELECT sum(pp.hours) AS sum
                   FROM purchasepayments pp
                  WHERE pp.purchaseid = p.id), 0::numeric)) * 60::numeric - COALESCE(( SELECT fn_get_used_hours.usedhours
                   FROM fn_get_used_hours(p.id) fn_get_used_hours(usedhours)), 0::numeric) AS rem_minutes,
            CURRENT_TIMESTAMP >= p.startdate AND CURRENT_TIMESTAMP <= p.enddate AS is_date_valid
           FROM purchases p
          ORDER BY p.customerid, (CURRENT_TIMESTAMP >= p.startdate AND CURRENT_TIMESTAMP <= p.enddate) DESC, (((p.hours + COALESCE(( SELECT sum(pp.hours) AS sum
                   FROM purchasepayments pp
                  WHERE pp.purchaseid = p.id), 0::numeric)) * 60::numeric - COALESCE(( SELECT fn_get_used_hours.usedhours
                   FROM fn_get_used_hours(p.id) fn_get_used_hours(usedhours)), 0::numeric)) > 0::numeric) DESC, p.enddate DESC
        )
 SELECT b.id,
    t.id AS table_id,
    jsonb_build_object('id', t.id::text, 'name', t.name) AS "table",
    c.id AS customer_id,
    ch.id AS child_id,
    b.starttime AS start_time,
    b.endtime AS end_time,
    b.subscriptionstarttime AS subscription_start_time,
    b.subscriptionendtime AS subscription_end_time,
    b.status,
    b.price,
    b.note,
    jsonb_build_object('id', ch.id::text, 'name', ch.name, 'parentId', c.id::text, 'parentName', c.name, 'parentLastName', c.lastname, 'phone', c.phone, 'status',
        CASE
            WHEN pbp.is_date_valid AND pbp.rem_minutes > 0::numeric THEN 'ActiveSubscriber'::text
            WHEN pbp.is_date_valid THEN 'OverageSubscriber'::text
            WHEN (EXISTS ( SELECT 1
               FROM purchases p3
              WHERE p3.customerid = c.id AND p3.startdate > CURRENT_TIMESTAMP)) THEN 'UpcomingSubscriber'::text
            WHEN (EXISTS ( SELECT 1
               FROM purchases p4
              WHERE p4.customerid = c.id)) THEN 'Subscriber'::text
            ELSE 'Customer'::text
        END, 'purchase', ( SELECT jsonb_build_object('id', p.id::text, 'hours', p.hours, 'price', p.price, 'startDate', p.startdate, 'endDate', p.enddate, 'customerId', p.customerid::text, 'usedHours', ( SELECT fn_get_used_hours.usedhours
                   FROM fn_get_used_hours(p.id) fn_get_used_hours(usedhours)), 'payments', ( SELECT COALESCE(jsonb_agg(jsonb_build_object('id', pp.id, 'purchaseId', pp.purchaseid, 'hours', pp.hours, 'price', pp.price)), '[]'::jsonb) AS "coalesce"
                   FROM purchasepayments pp
                  WHERE pp.purchaseid = p.id)) AS jsonb_build_object
           FROM purchases p
          WHERE p.id = pbp.purchase_id)) AS customer,
    ( SELECT jsonb_agg(jsonb_build_object('id', bl.id, 'bookingId', bl.bookingid, 'time', bl."time", 'type', bl.type, 'userId', bl.userid)) AS jsonb_agg
           FROM bookinglogs bl
          WHERE bl.bookingid = b.id) AS logs
   FROM bookings b
     LEFT JOIN children ch ON ch.id = b.child_id
     LEFT JOIN customers c ON c.id = ch.parent_id
     LEFT JOIN tables t ON t.id = b.tableid
     LEFT JOIN parent_best_package pbp ON c.id = pbp.customerid
  WHERE (b.status::text = ANY (ARRAY['Paused'::character varying, 'Active'::character varying]::text[])) AND (c.id IS NULL OR c.isdeleted = false) AND (ch.id IS NULL OR ch.is_deleted = false);

CREATE OR REPLACE VIEW public.vw_bookinglogs AS  SELECT id,
    bookingid,
    "time",
    type,
    userid AS user_id
   FROM bookinglogs;

CREATE OR REPLACE VIEW public.vw_companies AS  SELECT id,
    name,
    firsthourprice AS first_hour_price,
    additionalhalfhourprice AS additional_half_hour_price
   FROM company;

CREATE OR REPLACE VIEW public.vw_packages AS  SELECT id,
    name,
    hours,
    price,
    validity_days,
    is_deleted,
    created_at,
    updated_at
   FROM packages
  WHERE is_deleted = false;

CREATE OR REPLACE VIEW public.vw_purchases AS  SELECT id,
    hours::double precision AS hours,
    price::double precision AS price,
    customerid AS customer_id,
    startdate AS start_date,
    enddate AS end_date
   FROM purchases;

CREATE OR REPLACE VIEW public.vw_tables AS  SELECT id,
    name
   FROM tables
  WHERE isdeleted = false;

CREATE OR REPLACE VIEW public.vw_users AS  SELECT id,
    name,
    lastname AS last_name,
    phone,
    isactive AS is_active
   FROM users;

COMMIT;
