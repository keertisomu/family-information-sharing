#!/bin/bash
# Script to manually provision a global admin user in the Family Calendar system
# Usage: ./scripts/create-admin.sh <email>
# Example: ./scripts/create-admin.sh admin@example.com

set -e

# Check if email argument is provided
if [ -z "$1" ]; then
    echo "Usage: $0 <email>"
    echo "Example: $0 admin@example.com"
    exit 1
fi

EMAIL="$1"

# Get database connection details from environment or use defaults
DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-family_calendar}"
DB_USER="${DB_USER:-postgres}"
DB_PASSWORD="${DB_PASSWORD:-CHANGE_ME}"

echo "Creating global admin user with email: $EMAIL"
echo "Database: $DB_HOST:$DB_PORT/$DB_NAME"

# Check if user already exists
EXISTING_USER=$(PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -t -A -c \
    "SELECT email FROM users WHERE email = '$EMAIL';")

if [ ! -z "$EXISTING_USER" ]; then
    echo "User with email $EMAIL already exists. Updating to global admin..."
    PGPASSWORD="$DB_PASSWORD" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -c \
        "UPDATE users SET is_global_admin = true, updated_at = NOW() WHERE email = '$EMAIL';"
    echo "✓ User updated to global admin successfully"
else
    echo "Error: User with email $EMAIL not found."
    echo ""
    echo "Please note: Users must first authenticate via Google OAuth before they can be made global admins."
    echo "Steps:"
    echo "  1. Have the user visit /api/v1/auth/google/login and complete OAuth"
    echo "  2. Run this script again to grant global admin privileges"
    exit 1
fi

echo ""
echo "Global admin provisioning complete!"
echo "User $EMAIL now has global admin privileges."
