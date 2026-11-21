-- Member 4 / M6 Shelters.
IF SCHEMA_ID('shelter') IS NULL EXEC('CREATE SCHEMA shelter');
IF OBJECT_ID('shelter.Shelters','U') IS NULL
CREATE TABLE shelter.Shelters(
 Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, Name NVARCHAR(160) NOT NULL, Capacity INT NOT NULL, Occupancy INT NOT NULL DEFAULT 0,
 Facilities NVARCHAR(MAX) NOT NULL DEFAULT '[]', AccessibilityFeatures NVARCHAR(MAX) NOT NULL DEFAULT '[]',
 Services NVARCHAR(MAX) NOT NULL DEFAULT '[]', Lat FLOAT NOT NULL, Lng FLOAT NOT NULL, OrgId UNIQUEIDENTIFIER NOT NULL,
 Approved BIT NOT NULL DEFAULT 0, CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedAt DATETIME2 NULL,
 CONSTRAINT CK_Shelters_Capacity CHECK(Capacity>0), CONSTRAINT CK_Shelters_Occupancy CHECK(Occupancy>=0 AND Occupancy<=Capacity));
IF OBJECT_ID('shelter.ShelterAllocations','U') IS NULL
CREATE TABLE shelter.ShelterAllocations(
 Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, RequestId UNIQUEIDENTIFIER NOT NULL, ShelterId UNIQUEIDENTIFIER NOT NULL,
 HouseholdSize INT NOT NULL, DecisionId UNIQUEIDENTIFIER NOT NULL,
 CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedAt DATETIME2 NULL);

-- Member 4 integrity/index additions (safe to re-run).
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_ShelterAllocations_HouseholdSize')
 ALTER TABLE shelter.ShelterAllocations ADD CONSTRAINT CK_ShelterAllocations_HouseholdSize CHECK(HouseholdSize > 0);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Shelters_OrgId' AND object_id=OBJECT_ID('shelter.Shelters'))
 CREATE INDEX IX_Shelters_OrgId ON shelter.Shelters(OrgId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Shelters_Approved' AND object_id=OBJECT_ID('shelter.Shelters'))
 CREATE INDEX IX_Shelters_Approved ON shelter.Shelters(Approved);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShelterAllocations_ShelterId' AND object_id=OBJECT_ID('shelter.ShelterAllocations'))
 CREATE INDEX IX_ShelterAllocations_ShelterId ON shelter.ShelterAllocations(ShelterId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ShelterAllocations_RequestId' AND object_id=OBJECT_ID('shelter.ShelterAllocations'))
 CREATE INDEX IX_ShelterAllocations_RequestId ON shelter.ShelterAllocations(RequestId);
