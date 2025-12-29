using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CnabApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFileUploadStatusEnumValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert existing Status values from old enum to new enum
            // Old: {Success=1, Failed=2, Duplicate=3}
            // New: {Pending=0, Processing=1, Success=2, Failed=3, Duplicate=4}
            
            migrationBuilder.Sql(@"
                UPDATE ""FileUploads"" 
                SET ""Status"" = CASE 
                    WHEN ""Status"" = 1 THEN 2  -- Success: 1 -> 2
                    WHEN ""Status"" = 2 THEN 3  -- Failed: 2 -> 3
                    WHEN ""Status"" = 3 THEN 4  -- Duplicate: 3 -> 4
                    ELSE ""Status""
                END
                WHERE ""Status"" IN (1, 2, 3);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the conversion
            migrationBuilder.Sql(@"
                UPDATE ""FileUploads"" 
                SET ""Status"" = CASE 
                    WHEN ""Status"" = 2 THEN 1  -- Success: 2 -> 1
                    WHEN ""Status"" = 3 THEN 2  -- Failed: 3 -> 2
                    WHEN ""Status"" = 4 THEN 3  -- Duplicate: 4 -> 3
                    ELSE ""Status""
                END
                WHERE ""Status"" IN (2, 3, 4);
            ");
        }
    }
}
