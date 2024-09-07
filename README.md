# key-lock
Key Lock Mechanism

For MSSQL distributed locks a table with the following scheme is required

```sql
CREATE TABLE [dbo].[DistributedLocks](
	[LockKey] [nvarchar](50) NOT NULL,
	[ExpiresAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_DistributedLocks] PRIMARY KEY CLUSTERED 
(
	[LockKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 95, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
```
