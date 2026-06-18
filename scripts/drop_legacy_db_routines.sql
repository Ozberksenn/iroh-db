-- ============================================================
-- CLEANUP: drop legacy DB routines no longer used by the .NET
-- backend (all logic moved to EF/LINQ). Order: views -> procs -> funcs.
-- SAFETY: transactional; rollback via restore_legacy_db_routines.sql
-- or the full dumps iroh_db_backup.sql / iroh_db_updated_backup.sql.
-- Run ONLY after prod cutover is verified stable.
-- Base tables are NOT touched.
-- ============================================================
BEGIN;

-- Views
DROP VIEW IF EXISTS public.vw_activebookings CASCADE;
DROP VIEW IF EXISTS public.vw_bookinglogs CASCADE;
DROP VIEW IF EXISTS public.vw_companies CASCADE;
DROP VIEW IF EXISTS public.vw_packages CASCADE;
DROP VIEW IF EXISTS public.vw_purchases CASCADE;
DROP VIEW IF EXISTS public.vw_tables CASCADE;
DROP VIEW IF EXISTS public.vw_users CASCADE;

-- Procedures
DROP PROCEDURE IF EXISTS public.usp_create_user(IN p_name character varying, IN p_mail character varying, IN p_password text, IN p_phone character varying, IN p_lastname character varying);
DROP PROCEDURE IF EXISTS public.usp_delete_child(IN p_id bigint);
DROP PROCEDURE IF EXISTS public.usp_delete_customer(IN p_id bigint);
DROP PROCEDURE IF EXISTS public.usp_delete_package(IN p_id bigint);
DROP PROCEDURE IF EXISTS public.usp_delete_purchase(IN p_id bigint);
DROP PROCEDURE IF EXISTS public.usp_delete_table(IN p_id bigint);
DROP PROCEDURE IF EXISTS public.usp_insert_booking_log(IN p_bookingid bigint, IN p_time timestamp with time zone, IN p_type character varying, IN p_userid bigint);
DROP PROCEDURE IF EXISTS public.usp_insert_customer(IN p_name character varying, IN p_lastname character varying, IN p_parentname character varying, IN p_parentlastname character varying, IN p_phone character varying, IN p_mail character varying, IN p_parentphone character varying, IN p_parentmail character varying);
DROP PROCEDURE IF EXISTS public.usp_insert_customer(IN p_name character varying, IN p_lastname character varying, IN p_phone character varying, IN p_mail character varying);
DROP PROCEDURE IF EXISTS public.usp_insert_purchase(IN p_hours numeric, IN p_price numeric, IN p_customerid bigint, IN p_startdate timestamp with time zone, IN p_enddate timestamp with time zone);
DROP PROCEDURE IF EXISTS public.usp_insert_purchase_payment(IN p_purchaseid bigint, IN p_hours numeric, IN p_price numeric);
DROP PROCEDURE IF EXISTS public.usp_insert_table(IN p_name character varying);
DROP PROCEDURE IF EXISTS public.usp_update_booking(IN p_id bigint, IN p_tableid bigint, IN p_childid bigint, IN p_starttime timestamp with time zone, IN p_endtime timestamp with time zone, IN p_status character varying, IN p_price numeric, IN p_note text, IN p_purchaseid bigint, IN p_subscriptionstarttime timestamp with time zone, IN p_subscriptionendtime timestamp with time zone);
DROP PROCEDURE IF EXISTS public.usp_update_bookinglog(IN p_id bigint, IN p_bookingid bigint, IN p_time timestamp with time zone, IN p_type character varying, IN p_userid bigint);
DROP PROCEDURE IF EXISTS public.usp_update_child(IN p_id bigint, IN p_name character varying, IN p_birth_date date);
DROP PROCEDURE IF EXISTS public.usp_update_company(IN p_id bigint, IN p_name character varying, IN p_firsthourprice numeric, IN p_additionalhalfhourprice numeric);
DROP PROCEDURE IF EXISTS public.usp_update_customer(IN p_id bigint, IN p_name character varying, IN p_lastname character varying, IN p_parentname character varying, IN p_parentlastname character varying, IN p_phone character varying, IN p_mail character varying, IN p_parentphone character varying, IN p_parentmail character varying);
DROP PROCEDURE IF EXISTS public.usp_update_customer(IN p_id bigint, IN p_name character varying, IN p_lastname character varying, IN p_phone character varying, IN p_mail character varying);
DROP PROCEDURE IF EXISTS public.usp_update_package(IN p_id bigint, IN p_name character varying, IN p_hours numeric, IN p_price numeric, IN p_validity_days integer);
DROP PROCEDURE IF EXISTS public.usp_update_password(IN p_mail character varying, IN p_newpassword text);
DROP PROCEDURE IF EXISTS public.usp_update_purchase(IN p_id bigint, IN p_hours numeric, IN p_price numeric, IN p_customerid bigint, IN p_startdate timestamp with time zone, IN p_enddate timestamp with time zone);
DROP PROCEDURE IF EXISTS public.usp_update_table(IN p_id bigint, IN p_name character varying);

-- Functions (CASCADE: fn_search_unified & vw depend on fn_get_used_hours etc.)
DROP FUNCTION IF EXISTS public.fn_get_children_by_parent_id(p_parent_id bigint) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_customer_by_id(p_id bigint) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_customers(p_status character varying, p_page integer, p_size integer, p_name text, p_mail text) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_dashboard_busy_hours(p_start_date timestamp with time zone, p_end_date timestamp with time zone) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_dashboard_daily_list(p_start_date timestamp with time zone, p_end_date timestamp with time zone) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_dashboard_overview(p_start_date timestamp with time zone, p_end_date timestamp with time zone) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_dashboard_purchases(p_start_date timestamp with time zone, p_end_date timestamp with time zone) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_dashboard_revenue_chart(p_start_date timestamp with time zone, p_end_date timestamp with time zone) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_dashboard_top_customers(p_start_date timestamp with time zone, p_end_date timestamp with time zone) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_purchase_by_customer_id(p_customerid bigint) CASCADE;
DROP FUNCTION IF EXISTS public.fn_get_used_hours(p_purchaseid bigint) CASCADE;
DROP FUNCTION IF EXISTS public.fn_insert_booking(p_tableid bigint, p_starttime timestamp with time zone, p_endtime timestamp with time zone, p_status character varying, p_price numeric, p_childid bigint, p_note text) CASCADE;
DROP FUNCTION IF EXISTS public.fn_insert_child(p_parent_id bigint, p_name character varying, p_birth_date date) CASCADE;
DROP FUNCTION IF EXISTS public.fn_insert_package(p_name character varying, p_hours numeric, p_price numeric, p_validity_days integer) CASCADE;
DROP FUNCTION IF EXISTS public.fn_login(p_mail character varying) CASCADE;
DROP FUNCTION IF EXISTS public.fn_search_unified(p_search text) CASCADE;
DROP FUNCTION IF EXISTS public.usp_get_bookings(p_page integer, p_size integer, p_status text[], p_name text, p_mail text, p_customerid bigint, p_childid bigint, p_starttime timestamp with time zone, p_endtime timestamp with time zone) CASCADE;
DROP FUNCTION IF EXISTS public.usp_get_bookings(p_page integer, p_size integer, p_status text[], p_name text, p_mail text, p_customerid bigint, p_childid bigint, p_starttime timestamp with time zone, p_endtime timestamp with time zone, p_tableid bigint) CASCADE;
DROP FUNCTION IF EXISTS public.usp_get_purchase_bookings_by_id(p_purchaseid bigint) CASCADE;

COMMIT;
