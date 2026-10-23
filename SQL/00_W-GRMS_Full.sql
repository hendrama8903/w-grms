-- =====================================================================
-- W-GRMS : SCRIPT LENGKAP (gabungan 01 s/d 06)
-- Jalankan sekali di database GRMS. Aman dijalankan ulang (IF NOT EXISTS).
-- =====================================================================
USE [GRMS]
GO


-- ##### 01_CreateAuthTables.sql #####
-- =====================================================================
-- W-GRMS : tabel autentikasi & otorisasi (pola GRMS)
-- Catatan: W-GRMS TIDAK menambah stored procedure/view/function ke database;
-- semua query ada di kode (Models/Repo). Folder ini hanya berisi tabel & data awal.
--
-- TB_M_USER sudah ada di GRMS dan masih dipakai aplikasi desktop, jadi
-- TIDAK dibuat ulang - hanya ditambah kolom yang dibutuhkan web
-- (ROLE_ID, EMAIL, PHONE, DELETE_FLAG, audit). Desktop selalu INSERT
-- dengan column list, jadi kolom tambahan (semuanya nullable/default)
-- tidak mengganggu desktop.
--
-- Jalankan di database GRMS
-- =====================================================================

IF OBJECT_ID('dbo.TB_M_ROLE', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[TB_M_ROLE] (
		[ROLE_ID]    VARCHAR(50)  NOT NULL PRIMARY KEY,
		[ROLE_NAME]  VARCHAR(255) NULL,
		[ROLE_DESC]  VARCHAR(255) NULL,
		[CREATED_BY] VARCHAR(50)  NULL,
		[CREATED_DT] DATETIME     NULL,
		[CHANGED_BY] VARCHAR(50)  NULL,
		[CHANGED_DT] DATETIME     NULL
	)
END
GO

IF OBJECT_ID('dbo.TB_M_MENU', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[TB_M_MENU] (
		[MENU_ID]     VARCHAR(50)  NOT NULL PRIMARY KEY,
		[PARENT_ID]   VARCHAR(50)  NULL,
		[MENU_NAME]   VARCHAR(255) NULL,
		[MENU_ICON]   VARCHAR(255) NULL,
		[MENU_URL]    VARCHAR(255) NULL,
		[MENU_SEQ]    INT          NULL,
		[DELETE_FLAG] INT          NOT NULL DEFAULT 0,
		[CREATED_BY]  VARCHAR(50)  NULL,
		[CREATED_DT]  DATETIME     NULL,
		[CHANGED_BY]  VARCHAR(50)  NULL,
		[CHANGED_DT]  DATETIME     NULL
	)
END
GO

IF OBJECT_ID('dbo.TB_M_FUNCTION', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[TB_M_FUNCTION] (
		[FUNCTION_ID]   VARCHAR(50)  NOT NULL PRIMARY KEY,
		[MENU_ID]       VARCHAR(50)  NULL,
		[FUNCTION_NAME] VARCHAR(255) NULL,
		[FUNCTION_DESC] VARCHAR(255) NULL,
		[DELETE_FLAG]   INT          NOT NULL DEFAULT 0,
		[CREATED_BY]    VARCHAR(50)  NULL,
		[CREATED_DT]    DATETIME     NULL,
		[CHANGED_BY]    VARCHAR(50)  NULL,
		[CHANGED_DT]    DATETIME     NULL
	)
END
GO

IF OBJECT_ID('dbo.TB_M_AUTH_MENU', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[TB_M_AUTH_MENU] (
		[ROLE_ID]    VARCHAR(50) NOT NULL,
		[MENU_ID]    VARCHAR(50) NOT NULL,
		[CREATED_BY] VARCHAR(50) NULL,
		[CREATED_DT] DATETIME    NULL,
		[CHANGED_BY] VARCHAR(50) NULL,
		[CHANGED_DT] DATETIME    NULL,
		CONSTRAINT [PK_TB_M_AUTH_MENU] PRIMARY KEY ([ROLE_ID], [MENU_ID])
	)
END
GO

IF OBJECT_ID('dbo.TB_M_AUTH_FUNCTION', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[TB_M_AUTH_FUNCTION] (
		[ROLE_ID]     VARCHAR(50) NOT NULL,
		[FUNCTION_ID] VARCHAR(50) NOT NULL,
		[CREATED_BY]  VARCHAR(50) NULL,
		[CREATED_DT]  DATETIME    NULL,
		[CHANGED_BY]  VARCHAR(50) NULL,
		[CHANGED_DT]  DATETIME    NULL,
		CONSTRAINT [PK_TB_M_AUTH_FUNCTION] PRIMARY KEY ([ROLE_ID], [FUNCTION_ID])
	)
END
GO

IF OBJECT_ID('dbo.TB_R_LOG_H', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[TB_R_LOG_H] (
		[PROCESS_ID]     BIGINT       NOT NULL PRIMARY KEY,
		[MODULE]         VARCHAR(255) NULL,
		[FUNCTION]       VARCHAR(255) NULL,
		[START_DT]       DATETIME     NULL,
		[END_DT]         DATETIME     NULL,
		[PROCESS_STATUS] CHAR(1)      NULL,
		[CREATED_BY]     VARCHAR(50)  NULL,
		[CREATED_DT]     DATETIME     NULL,
		[CHANGED_BY]     VARCHAR(50)  NULL,
		[CHANGED_DT]     DATETIME     NULL
	)
END
GO

IF OBJECT_ID('dbo.TB_R_LOG_D', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[TB_R_LOG_D] (
		[PROCESS_ID]      BIGINT       NOT NULL,
		[SEQ_NO]          INT          NOT NULL,
		[MESSAGE_TYPE]    CHAR(3)      NULL,
		[MESSAGE_CONTENT] VARCHAR(MAX) NULL,
		[LOCATION]        VARCHAR(255) NULL,
		[CREATED_BY]      VARCHAR(50)  NULL,
		[CREATED_DT]      DATETIME     NULL,
		[CHANGED_BY]      VARCHAR(50)  NULL,
		[CHANGED_DT]      DATETIME     NULL,
		CONSTRAINT [PK_TB_R_LOG_D] PRIMARY KEY ([PROCESS_ID], [SEQ_NO])
	)
END
GO

-- Kolom tambahan TB_M_USER untuk web
IF COL_LENGTH('dbo.TB_M_USER', 'ROLE_ID') IS NULL
	ALTER TABLE [dbo].[TB_M_USER] ADD [ROLE_ID] VARCHAR(50) NULL;
IF COL_LENGTH('dbo.TB_M_USER', 'EMAIL') IS NULL
	ALTER TABLE [dbo].[TB_M_USER] ADD [EMAIL] VARCHAR(255) NULL;
IF COL_LENGTH('dbo.TB_M_USER', 'PHONE') IS NULL
	ALTER TABLE [dbo].[TB_M_USER] ADD [PHONE] VARCHAR(15) NULL;
IF COL_LENGTH('dbo.TB_M_USER', 'DELETE_FLAG') IS NULL
	ALTER TABLE [dbo].[TB_M_USER] ADD [DELETE_FLAG] CHAR(1) NOT NULL CONSTRAINT [DF_TB_M_USER_DELETE_FLAG] DEFAULT ('0');
IF COL_LENGTH('dbo.TB_M_USER', 'CREATED_BY') IS NULL
	ALTER TABLE [dbo].[TB_M_USER] ADD [CREATED_BY] VARCHAR(50) NULL;
IF COL_LENGTH('dbo.TB_M_USER', 'CREATED_DT') IS NULL
	ALTER TABLE [dbo].[TB_M_USER] ADD [CREATED_DT] DATETIME NULL;
IF COL_LENGTH('dbo.TB_M_USER', 'CHANGED_BY') IS NULL
	ALTER TABLE [dbo].[TB_M_USER] ADD [CHANGED_BY] VARCHAR(50) NULL;
IF COL_LENGTH('dbo.TB_M_USER', 'CHANGED_DT') IS NULL
	ALTER TABLE [dbo].[TB_M_USER] ADD [CHANGED_DT] DATETIME NULL;
GO

GO

-- ##### 02_Seed_MenuRole.sql #####
-- =====================================================================
-- W-GRMS : data awal role, menu, function & otorisasi
-- Menu bisnis (Master/Transaction/Report/SAP Interface) ditambahkan
-- per modul saat modulnya selesai dibuat.
--
-- Jalankan di database GRMS (aman dijalankan ulang)
-- =====================================================================

-- Role
IF NOT EXISTS (SELECT 1 FROM TB_M_ROLE WHERE ROLE_ID = 'ADMIN')
	INSERT INTO TB_M_ROLE (ROLE_ID, ROLE_NAME, ROLE_DESC, CREATED_BY, CREATED_DT)
	VALUES ('ADMIN', 'Administrator', 'Full access to all GRMS menus', 'system', GETDATE());

IF NOT EXISTS (SELECT 1 FROM TB_M_ROLE WHERE ROLE_ID = 'USER')
	INSERT INTO TB_M_ROLE (ROLE_ID, ROLE_NAME, ROLE_DESC, CREATED_BY, CREATED_DT)
	VALUES ('USER', 'GRMS User', 'Operational user (Goods Receiving)', 'system', GETDATE());
GO

-- Menu
MERGE TB_M_MENU AS T
USING (VALUES
	('M00001',    NULL,     'Dashboard',          'fa-dashboard', '/Home/Index',          1),
	('M00009',    NULL,     'System',             'fa-cogs',      '#',                    9),
	('M00009-01', 'M00009', 'User Management',    'fa-users',     '/User/Index',          1),
	('M00009-02', 'M00009', 'Role Authorization', 'fa-key',       '/Role/Index',          2),
	('M00009-03', 'M00009', 'Menu & Function',    'fa-sitemap',   '/Menu/Index',          3),
	('M00009-04', 'M00009', 'Activity Log',       'fa-history',   '/LogMonitoring/Index', 4)
) AS S (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ)
ON T.MENU_ID = S.MENU_ID
WHEN NOT MATCHED THEN
	INSERT (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.MENU_ID, S.PARENT_ID, S.MENU_NAME, S.MENU_ICON, S.MENU_URL, S.MENU_SEQ, 0, 'system', GETDATE());
GO

-- Function
MERGE TB_M_FUNCTION AS T
USING (VALUES
	('USER-ADD',           'M00009-01', 'Add',           'Add user'),
	('USER-EDIT',          'M00009-01', 'Edit',          'Edit user'),
	('USER-DELETE',        'M00009-01', 'Delete',        'Delete user'),
	('USER-RESTORE',       'M00009-01', 'Restore',       'Restore deleted user'),
	('ROLE-ADD',           'M00009-02', 'Add',           'Add role'),
	('ROLE-EDIT',          'M00009-02', 'Edit',          'Edit role'),
	('ROLE-DELETE',        'M00009-02', 'Delete',        'Delete role'),
	('ROLE-AUTHORIZATION', 'M00009-02', 'Authorization', 'Set role menu & function access'),
	('MENU-ADD',           'M00009-03', 'Add',           'Add menu'),
	('MENU-EDIT',          'M00009-03', 'Edit',          'Edit menu'),
	('MENU-DELETE',        'M00009-03', 'Delete',        'Delete menu'),
	('MENU-FUNCTION',      'M00009-03', 'Function',      'Maintain menu functions')
) AS S (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC)
ON T.FUNCTION_ID = S.FUNCTION_ID
WHEN NOT MATCHED THEN
	INSERT (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.FUNCTION_ID, S.MENU_ID, S.FUNCTION_NAME, S.FUNCTION_DESC, 0, 'system', GETDATE());
GO

-- ADMIN: semua menu & function
INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT 'ADMIN', M.MENU_ID, 'system', GETDATE()
FROM TB_M_MENU M
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU A WHERE A.ROLE_ID = 'ADMIN' AND A.MENU_ID = M.MENU_ID);

INSERT INTO TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT 'ADMIN', F.FUNCTION_ID, 'system', GETDATE()
FROM TB_M_FUNCTION F
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = 'ADMIN' AND A.FUNCTION_ID = F.FUNCTION_ID);

-- USER: dashboard
IF NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU WHERE ROLE_ID = 'USER' AND MENU_ID = 'M00001')
	INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
	VALUES ('USER', 'M00001', 'system', GETDATE());
GO

-- User lama: IT Application = ADMIN, sisanya USER
UPDATE TB_M_USER
SET ROLE_ID = CASE WHEN DEPT LIKE 'IT Application%' THEN 'ADMIN' ELSE 'USER' END
WHERE ROLE_ID IS NULL;
GO

GO

-- ##### 03_Seed_MasterData.sql #####
-- =====================================================================
-- W-GRMS : menu & function Master Data (CKB)
--   Parts Master (CKB)    -> OLKPAM
--   Supplier Master (CKB) -> OLKSUP
-- Sumber "Update from CKB" (linked server per plant) ada di appsettings.json "CkbSources".
--
-- Jalankan di database GRMS (aman dijalankan ulang)
-- =====================================================================

MERGE TB_M_MENU AS T
USING (VALUES
	('M00002',    NULL,     'Master Data',           'fa-database', '#',                       2),
	('M00002-01', 'M00002', 'Parts Master (CKB)',    'fa-cubes',    '/PartsMaster/Index',      1),
	('M00002-02', 'M00002', 'Supplier Master (CKB)', 'fa-truck',    '/SupplierMaster/Index',   2)
) AS S (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ)
ON T.MENU_ID = S.MENU_ID
WHEN NOT MATCHED THEN
	INSERT (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.MENU_ID, S.PARENT_ID, S.MENU_NAME, S.MENU_ICON, S.MENU_URL, S.MENU_SEQ, 0, 'system', GETDATE());
GO

MERGE TB_M_FUNCTION AS T
USING (VALUES
	('PARTS-SYNC',      'M00002-01', 'Update',  'Update parts from CKB database'),
	('PARTS-DELETE',    'M00002-01', 'Delete',  'Delete part'),
	('SUPPLIER-SYNC',   'M00002-02', 'Update',  'Update suppliers from CKB databases'),
	('SUPPLIER-DELETE', 'M00002-02', 'Delete',  'Delete supplier')
) AS S (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC)
ON T.FUNCTION_ID = S.FUNCTION_ID
WHEN NOT MATCHED THEN
	INSERT (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.FUNCTION_ID, S.MENU_ID, S.FUNCTION_NAME, S.FUNCTION_DESC, 0, 'system', GETDATE());
GO

-- ADMIN: semua. USER: lihat + update dari CKB (hapus hanya ADMIN)
INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, M.MENU_ID, 'system', GETDATE()
FROM (VALUES ('ADMIN'), ('USER')) R (ROLE_ID)
CROSS JOIN TB_M_MENU M
WHERE M.MENU_ID IN ('M00002', 'M00002-01', 'M00002-02')
  AND NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU A WHERE A.ROLE_ID = R.ROLE_ID AND A.MENU_ID = M.MENU_ID);

INSERT INTO TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT X.ROLE_ID, X.FUNCTION_ID, 'system', GETDATE()
FROM (VALUES
	('ADMIN', 'PARTS-SYNC'), ('ADMIN', 'PARTS-DELETE'), ('ADMIN', 'SUPPLIER-SYNC'), ('ADMIN', 'SUPPLIER-DELETE'),
	('USER', 'PARTS-SYNC'), ('USER', 'SUPPLIER-SYNC')
) X (ROLE_ID, FUNCTION_ID)
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = X.ROLE_ID AND A.FUNCTION_ID = X.FUNCTION_ID);
GO

GO

-- ##### 04_Seed_Transaction.sql #####
-- =====================================================================
-- W-GRMS : menu & function Transaction (CKB)
--   Order Data (CKB)              -> OLKKBNORD (download dari database CKB plant)
--   Goods Receiving (CKB)         -> TB_T_RECEIVE_H / TB_T_RECEIVE_D (+ DN sisa di OLKKBNORD)
--   Delivery Note Printing (CKB)  -> cetak DN sisa (DN#n)
--   Data SOC (CKB)                -> OLKSOC langsung dari database CKB plant
-- Koneksi CKB per plant ada di appsettings.json "CkbSources".
--
-- Jalankan di database GRMS (aman dijalankan ulang)
-- =====================================================================

MERGE TB_M_MENU AS T
USING (VALUES
	('M00003',    NULL,     'Transaction',                   'fa-exchange',       '#',                          3),
	('M00003-01', 'M00003', 'Order Data (CKB)',              'fa-cloud-download', '/OrderData/Index',            1),
	('M00003-02', 'M00003', 'Goods Receiving (CKB)',         'fa-inbox',          '/GoodsReceiving/Index',       2),
	('M00003-03', 'M00003', 'Delivery Note Printing (CKB)',  'fa-print',          '/DeliveryNotePrinting/Index', 3),
	('M00003-05', 'M00003', 'Data SOC (CKB)',                'fa-table',          '/DataSoc/Index',              5)
) AS S (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ)
ON T.MENU_ID = S.MENU_ID
WHEN NOT MATCHED THEN
	INSERT (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.MENU_ID, S.PARENT_ID, S.MENU_NAME, S.MENU_ICON, S.MENU_URL, S.MENU_SEQ, 0, 'system', GETDATE());
GO

MERGE TB_M_FUNCTION AS T
USING (VALUES
	('ORDER-DOWNLOAD', 'M00003-01', 'Download', 'Download order data from CKB database'),
	('ORDER-DELETE',   'M00003-01', 'Delete',   'Delete order data that has not been received'),
	('GR-SAVE',        'M00003-02', 'Save',     'Save goods receive'),
	('GR-DELETE',      'M00003-02', 'Delete',   'Delete goods receive')
) AS S (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC)
ON T.FUNCTION_ID = S.FUNCTION_ID
WHEN NOT MATCHED THEN
	INSERT (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.FUNCTION_ID, S.MENU_ID, S.FUNCTION_NAME, S.FUNCTION_DESC, 0, 'system', GETDATE());
GO

-- ADMIN: semua menu & function Transaction
-- USER : Order Data (download), Goods Receiving (simpan & hapus), DN Printing, Data SOC.
--        Hapus Order Data hanya ADMIN (di desktop hanya sebagian kecil user yang punya akses ini).
INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT X.ROLE_ID, X.MENU_ID, 'system', GETDATE()
FROM (VALUES
	('ADMIN', 'M00003'), ('ADMIN', 'M00003-01'), ('ADMIN', 'M00003-02'), ('ADMIN', 'M00003-03'), ('ADMIN', 'M00003-05'),
	('USER',  'M00003'), ('USER',  'M00003-01'), ('USER',  'M00003-02'), ('USER',  'M00003-03'), ('USER',  'M00003-05')
) X (ROLE_ID, MENU_ID)
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU A WHERE A.ROLE_ID = X.ROLE_ID AND A.MENU_ID = X.MENU_ID);

INSERT INTO TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT X.ROLE_ID, X.FUNCTION_ID, 'system', GETDATE()
FROM (VALUES
	('ADMIN', 'ORDER-DOWNLOAD'), ('ADMIN', 'ORDER-DELETE'), ('ADMIN', 'GR-SAVE'), ('ADMIN', 'GR-DELETE'),
	('USER',  'ORDER-DOWNLOAD'), ('USER',  'GR-SAVE'), ('USER',  'GR-DELETE')
) X (ROLE_ID, FUNCTION_ID)
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = X.ROLE_ID AND A.FUNCTION_ID = X.FUNCTION_ID);
GO

GO


-- Menu Delivery Note Cleansing (CKB) sudah tidak dipakai - bersihkan bila sempat terpasang
DELETE FROM TB_M_AUTH_FUNCTION WHERE FUNCTION_ID = 'DNCLEAN-DELETE';
DELETE FROM TB_M_FUNCTION WHERE FUNCTION_ID = 'DNCLEAN-DELETE';
DELETE FROM TB_M_AUTH_MENU WHERE MENU_ID = 'M00003-04';
DELETE FROM TB_M_MENU WHERE MENU_ID = 'M00003-04';
GO

-- ##### 05_Seed_Report.sql #####
-- =====================================================================
-- W-GRMS : menu Report (CKB)
--   Goods Receiving History (CKB), Outstanding Order (CKB), Delivery Delay Summary (CKB)
-- Report hanya baca data, jadi tidak ada function (tombol) khusus.
--
-- Jalankan di database GRMS (aman dijalankan ulang)
-- =====================================================================

MERGE TB_M_MENU AS T
USING (VALUES
	('M00004',    NULL,     'Report',                         'fa-bar-chart',      '#',                       4),
	('M00004-01', 'M00004', 'Goods Receiving History (CKB)',  'fa-history',        '/GrHistory/Index',        1),
	('M00004-02', 'M00004', 'Outstanding Order (CKB)',        'fa-hourglass-half', '/OutstandingOrder/Index', 2),
	('M00004-03', 'M00004', 'Delivery Delay Summary (CKB)',   'fa-clock-o',        '/SummaryDelay/Index',     3)
) AS S (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ)
ON T.MENU_ID = S.MENU_ID
WHEN NOT MATCHED THEN
	INSERT (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.MENU_ID, S.PARENT_ID, S.MENU_NAME, S.MENU_ICON, S.MENU_URL, S.MENU_SEQ, 0, 'system', GETDATE());
GO

-- ADMIN & USER: semua report (di desktop semua user punya akses report)
INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, M.MENU_ID, 'system', GETDATE()
FROM (VALUES ('ADMIN'), ('USER')) R (ROLE_ID)
CROSS JOIN TB_M_MENU M
WHERE M.MENU_ID IN ('M00004', 'M00004-01', 'M00004-02', 'M00004-03')
  AND NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU A WHERE A.ROLE_ID = R.ROLE_ID AND A.MENU_ID = M.MENU_ID);
GO

GO

-- Nama menu lama "Summary Delay Delivery (CKB)" diganti ke ejaan yang benar
UPDATE TB_M_MENU SET MENU_NAME = 'Delivery Delay Summary (CKB)', CHANGED_BY = 'system', CHANGED_DT = GETDATE()
WHERE MENU_ID = 'M00004-03' AND MENU_NAME = 'Summary Delay Delivery (CKB)';
GO

-- ##### 06_ReceivingArea.sql #####
-- =====================================================================
-- W-GRMS : master Receiving Area (kode area receive CKB = RACRAC)
--   TB_M_RECEIVING_AREA -> daftar kode area receive + nama + plant CKB.
--   Dipakai sebagai pilihan "Receiving Areas" di User Management
--   (disimpan ke TB_M_USER_PLANT.PLANTCODE yang dipakai bersama desktop).
--
-- Jalankan di database GRMS (aman dijalankan ulang)
-- =====================================================================

IF OBJECT_ID('dbo.TB_M_RECEIVING_AREA', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[TB_M_RECEIVING_AREA] (
		[AREA_CODE]  VARCHAR(4)   NOT NULL PRIMARY KEY,
		[AREA_NAME]  VARCHAR(100) NULL,
		[PLANT_CODE] VARCHAR(5)   NULL,
		[CREATED_BY] VARCHAR(50)  NULL,
		[CREATED_DT] DATETIME     NULL,
		[CHANGED_BY] VARCHAR(50)  NULL,
		[CHANGED_DT] DATETIME     NULL
	)
END
GO

-- Data awal: kode area dari Parts Master CKB (OLKPAM) beserta plant-nya,
-- lalu kode yang sudah dipakai user tapi belum ada di OLKPAM.
INSERT INTO [dbo].[TB_M_RECEIVING_AREA] (AREA_CODE, PLANT_CODE, CREATED_BY, CREATED_DT)
SELECT P.RACRAC, MIN(P.PLCPCD), 'system', GETDATE()
FROM [dbo].[OLKPAM] P
WHERE ISNULL(P.RACRAC, '') <> ''
  AND NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_RECEIVING_AREA] A WHERE A.AREA_CODE = P.RACRAC)
GROUP BY P.RACRAC;

INSERT INTO [dbo].[TB_M_RECEIVING_AREA] (AREA_CODE, CREATED_BY, CREATED_DT)
SELECT DISTINCT U.PLANTCODE, 'system', GETDATE()
FROM [dbo].[TB_M_USER_PLANT] U
WHERE ISNULL(U.PLANTCODE, '') <> ''
  AND NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_RECEIVING_AREA] A WHERE A.AREA_CODE = U.PLANTCODE);
GO

MERGE TB_M_MENU AS T
USING (VALUES
	('M00002-03', 'M00002', 'Receiving Area', 'fa-map-marker', '/ReceivingArea/Index', 3)
) AS S (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ)
ON T.MENU_ID = S.MENU_ID
WHEN NOT MATCHED THEN
	INSERT (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.MENU_ID, S.PARENT_ID, S.MENU_NAME, S.MENU_ICON, S.MENU_URL, S.MENU_SEQ, 0, 'system', GETDATE());
GO

MERGE TB_M_FUNCTION AS T
USING (VALUES
	('RECAREA-SAVE',   'M00002-03', 'Save',   'Add or edit receiving area'),
	('RECAREA-DELETE', 'M00002-03', 'Delete', 'Delete receiving area')
) AS S (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC)
ON T.FUNCTION_ID = S.FUNCTION_ID
WHEN NOT MATCHED THEN
	INSERT (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.FUNCTION_ID, S.MENU_ID, S.FUNCTION_NAME, S.FUNCTION_DESC, 0, 'system', GETDATE());
GO

-- Hanya ADMIN (master ini dipakai untuk mengatur akses user)
INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT 'ADMIN', 'M00002-03', 'system', GETDATE()
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU A WHERE A.ROLE_ID = 'ADMIN' AND A.MENU_ID = 'M00002-03');

INSERT INTO TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT X.ROLE_ID, X.FUNCTION_ID, 'system', GETDATE()
FROM (VALUES ('ADMIN', 'RECAREA-SAVE'), ('ADMIN', 'RECAREA-DELETE')) X (ROLE_ID, FUNCTION_ID)
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = X.ROLE_ID AND A.FUNCTION_ID = X.FUNCTION_ID);
GO

GO
