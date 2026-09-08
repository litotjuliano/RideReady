# RideReady PHP (Laravel) Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a standalone Laravel 11 application in `RideReady - PHP/` that replicates phase 1 of RideReady: guest customer booking with fare quoting, a WhatsApp notice to the operator on booking creation, and admin login with a read-only bookings dashboard.

**Architecture:** Laravel 11 MVC monolith (Blade views, no SPA/Vite build required), MySQL/MariaDB for dev, SQLite in-memory for the test suite. Two Eloquent-backed guards: the default `web` guard is unused; a dedicated `admin` guard authenticates against an `admins` table. Business logic lives in plain service classes (`BookingService`, `MockDistanceService`, `WhatsAppService`) injected into controllers — no queues, no jobs, no external API calls in tests.

**Tech Stack:** PHP 8.2, Laravel 11, MySQL/MariaDB (dev), SQLite (tests), PHPUnit via `php artisan test`, Laravel's `Http` facade for the WhatsApp Cloud API call.

**Spec:** `docs/superpowers/specs/2026-09-08-rideready-php-phase1-design.md`

---

## Environment Notes (read before starting)

- PHP 8.2.12, Composer 2.7.6, and the `laravel/installer` are already available. `pdo_mysql` and `pdo_sqlite` extensions are installed.
- MariaDB (via XAMPP) is **not running by default**. Start it with `cmd //c "C:\\xampp\\mysql_start.bat"` (run this with a tool that supports `run_in_background: true` — the script opens a console window and blocks for as long as the server runs). Wait ~3 seconds, then verify with `mysql -u root -pYMUbbi1mwD -e "SELECT VERSION();"`.
- The local MariaDB root password is `YMUbbi1mwD` (read from `C:\xampp\passwords.txt` — this is a local-only dev credential, not a secret to protect).
- All commands below assume a POSIX-style shell (Git Bash). The project path contains a space (`RideReady - PHP`) — always quote it.
- All commands in this plan run from `/c/LitXus Systems/RideReady` unless a step says to `cd` into the new project first.

---

### Task 1: Scaffold the Laravel application

**Files:**
- Create: `RideReady - PHP/` (full Laravel 11 skeleton, via Composer)

- [ ] **Step 1: Scaffold the project**

Run:
```bash
cd "/c/LitXus Systems/RideReady"
composer create-project laravel/laravel "RideReady - PHP" "^11.0" --no-interaction
```
Expected: Composer installs Laravel 11.x and its dependencies into `RideReady - PHP/`, then runs `php artisan key:generate` automatically (you'll see "Application key set successfully." near the end).

- [ ] **Step 2: Verify the default test suite passes**

Run:
```bash
cd "/c/LitXus Systems/RideReady/RideReady - PHP"
php artisan test
```
Expected: `Tests:  2 passed` (the default `ExampleTest` in `tests/Unit` and `tests/Feature`).

- [ ] **Step 3: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP"
git commit -m "feat(php): scaffold Laravel 11 application for RideReady PHP clone"
```

---

### Task 2: Configure environment, database, and test runner

**Files:**
- Modify: `RideReady - PHP/.env`
- Modify: `RideReady - PHP/.env.example`
- Modify: `RideReady - PHP/phpunit.xml`

- [ ] **Step 1: Start MariaDB and create the dev database**

Run (use a tool call that supports `run_in_background: true`, since the script blocks):
```bash
cmd //c "C:\\xampp\\mysql_start.bat"
```
Then, in a separate command, wait a few seconds and verify:
```bash
mysql -u root -pYMUbbi1mwD -e "CREATE DATABASE IF NOT EXISTS rideready_php;"
mysql -u root -pYMUbbi1mwD -e "SHOW DATABASES LIKE 'rideready_php';"
```
Expected: the second command prints a row showing `rideready_php`. Skip the start step if the server is already running (the create/verify commands will just succeed against the running instance).

- [ ] **Step 2: Point the app at MySQL**

In `RideReady - PHP/.env`, find the `DB_CONNECTION=sqlite` line and the commented `DB_HOST`/`DB_PORT`/`DB_DATABASE`/`DB_USERNAME`/`DB_PASSWORD` lines below it. Replace that whole block with:

```
DB_CONNECTION=mysql
DB_HOST=127.0.0.1
DB_PORT=3306
DB_DATABASE=rideready_php
DB_USERNAME=root
DB_PASSWORD=YMUbbi1mwD
```

Make the same change in `RideReady - PHP/.env.example`, except leave the password blank (`DB_PASSWORD=`) so the example file doesn't carry a real credential.

- [ ] **Step 2: Add admin and WhatsApp config to `.env`**

At the end of `RideReady - PHP/.env`, add:

```

ADMIN_USERNAME=admin
ADMIN_PASSWORD=change-me-please

WHATSAPP_API_URL=https://graph.facebook.com/v20.0
WHATSAPP_ACCESS_TOKEN=
WHATSAPP_PHONE_NUMBER_ID=
WHATSAPP_OPERATOR_PHONE=
```

Add the same block to `RideReady - PHP/.env.example`, but with blank/placeholder values throughout (`ADMIN_USERNAME=`, `ADMIN_PASSWORD=`, and the four `WHATSAPP_*` keys all blank) so no real credentials live in the example file.

- [ ] **Step 3: Configure the test environment**

In `RideReady - PHP/phpunit.xml`, find these two commented-out lines inside `<php>`:
```xml
        <!-- <env name="DB_CONNECTION" value="sqlite"/> -->
        <!-- <env name="DB_DATABASE" value=":memory:"/> -->
```
Uncomment them:
```xml
        <env name="DB_CONNECTION" value="sqlite"/>
        <env name="DB_DATABASE" value=":memory:"/>
```
Then add two more `<env>` lines right after them, so the test suite always seeds a known, fixed admin (independent of whatever is in the real `.env`):
```xml
        <env name="ADMIN_USERNAME" value="test-admin"/>
        <env name="ADMIN_PASSWORD" value="test-pass-123"/>
```

- [ ] **Step 4: Verify the test suite still passes against the new config**

Run:
```bash
cd "/c/LitXus Systems/RideReady/RideReady - PHP"
php artisan test
```
Expected: `Tests:  2 passed` (still the default tests — this just confirms the SQLite-in-memory test DB boots correctly).

- [ ] **Step 5: Verify the app boots against MySQL**

Run:
```bash
php artisan migrate
```
Expected: creates `users`, `password_reset_tokens`, `sessions`, `cache`, `cache_locks`, `jobs`, `job_batches`, and `failed_jobs` tables in `rideready_php` with no errors (these are Laravel's unused default tables — harmless, left in place).

- [ ] **Step 6: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/.env.example" "RideReady - PHP/phpunit.xml"
git commit -m "feat(php): configure MySQL, admin/WhatsApp env vars, and SQLite test DB"
```

(`.env` itself is gitignored by Laravel's default `.gitignore` and won't be committed — that's expected.)

---

### Task 3: Admin authentication guard, model, and migration

**Files:**
- Create: `RideReady - PHP/app/Models/Admin.php`
- Create: `RideReady - PHP/database/migrations/<timestamp>_create_admins_table.php`
- Modify: `RideReady - PHP/config/auth.php`
- Test: `RideReady - PHP/tests/Feature/AdminGuardTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/AdminGuardTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Models\Admin;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\Auth;
use Tests\TestCase;

class AdminGuardTest extends TestCase
{
    use RefreshDatabase;

    public function test_admin_guard_authenticates_against_the_admins_table(): void
    {
        Admin::create(['username' => 'operator', 'password' => 'secret123']);

        $ok = Auth::guard('admin')->attempt(['username' => 'operator', 'password' => 'secret123']);

        $this->assertTrue($ok);
        $this->assertTrue(Auth::guard('admin')->check());
    }

    public function test_admin_guard_rejects_the_wrong_password(): void
    {
        Admin::create(['username' => 'operator', 'password' => 'secret123']);

        $ok = Auth::guard('admin')->attempt(['username' => 'operator', 'password' => 'wrong']);

        $this->assertFalse($ok);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Feature/AdminGuardTest.php`
Expected: FAIL — `Class "App\Models\Admin" not found`.

- [ ] **Step 3: Create the migration**

Run: `php artisan make:migration create_admins_table --create=admins`

This creates `database/migrations/<timestamp>_create_admins_table.php`. Open it and replace the `up()`/`down()` methods:

```php
    public function up(): void
    {
        Schema::create('admins', function (Blueprint $table) {
            $table->id();
            $table->string('username')->unique();
            $table->string('password');
            $table->rememberToken();
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('admins');
    }
```

- [ ] **Step 4: Create the Admin model**

Create `RideReady - PHP/app/Models/Admin.php`:

```php
<?php

namespace App\Models;

use Illuminate\Foundation\Auth\User as Authenticatable;

class Admin extends Authenticatable
{
    protected $fillable = ['username', 'password'];

    protected $hidden = ['password', 'remember_token'];

    protected $casts = [
        'password' => 'hashed',
    ];
}
```

- [ ] **Step 5: Register the `admin` guard**

In `RideReady - PHP/config/auth.php`, add an `admin` entry to `guards` (alongside the existing `web` entry):

```php
        'admin' => [
            'driver' => 'session',
            'provider' => 'admins',
        ],
```

And add an `admins` entry to `providers` (alongside the existing `users` entry):

```php
        'admins' => [
            'driver' => 'eloquent',
            'model' => App\Models\Admin::class,
        ],
```

- [ ] **Step 6: Run migrations and the test again**

Run:
```bash
php artisan migrate
php artisan test tests/Feature/AdminGuardTest.php
```
Expected: `Tests:  2 passed`.

- [ ] **Step 7: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Models/Admin.php" "RideReady - PHP/database/migrations" "RideReady - PHP/config/auth.php" "RideReady - PHP/tests/Feature/AdminGuardTest.php"
git commit -m "feat(php): add Admin model, admins table, and admin auth guard"
```

---

### Task 4: Admin seeder

**Files:**
- Create: `RideReady - PHP/database/seeders/AdminSeeder.php`
- Test: `RideReady - PHP/tests/Feature/AdminSeederTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/AdminSeederTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Models\Admin;
use Database\Seeders\AdminSeeder;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\Hash;
use Tests\TestCase;

class AdminSeederTest extends TestCase
{
    use RefreshDatabase;

    public function test_seeds_exactly_one_admin_from_env(): void
    {
        (new AdminSeeder())->run();

        $this->assertSame(1, Admin::count());

        $admin = Admin::first();
        $this->assertSame('test-admin', $admin->username);
        $this->assertTrue(Hash::check('test-pass-123', $admin->password));
    }

    public function test_running_it_twice_does_not_duplicate_the_admin(): void
    {
        (new AdminSeeder())->run();
        (new AdminSeeder())->run();

        $this->assertSame(1, Admin::count());
    }
}
```

(`ADMIN_USERNAME=test-admin` / `ADMIN_PASSWORD=test-pass-123` come from the `phpunit.xml` env overrides added in Task 2.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Feature/AdminSeederTest.php`
Expected: FAIL — `Class "Database\Seeders\AdminSeeder" not found`.

- [ ] **Step 3: Create the seeder**

Create `RideReady - PHP/database/seeders/AdminSeeder.php`:

```php
<?php

namespace Database\Seeders;

use App\Models\Admin;
use Illuminate\Database\Seeder;

class AdminSeeder extends Seeder
{
    public function run(): void
    {
        Admin::firstOrCreate(
            ['username' => env('ADMIN_USERNAME', 'admin')],
            ['password' => env('ADMIN_PASSWORD', 'password')],
        );
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `php artisan test tests/Feature/AdminSeederTest.php`
Expected: `Tests:  2 passed`.

- [ ] **Step 5: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/database/seeders/AdminSeeder.php" "RideReady - PHP/tests/Feature/AdminSeederTest.php"
git commit -m "feat(php): add AdminSeeder to seed one admin from env"
```

---

### Task 5: Customer model, migration, and factory

**Files:**
- Create: `RideReady - PHP/app/Models/Customer.php`
- Create: `RideReady - PHP/database/migrations/<timestamp>_create_customers_table.php`
- Create: `RideReady - PHP/database/factories/CustomerFactory.php`
- Test: `RideReady - PHP/tests/Feature/CustomerTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/CustomerTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Models\Customer;
use Illuminate\Database\QueryException;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class CustomerTest extends TestCase
{
    use RefreshDatabase;

    public function test_factory_creates_a_valid_customer(): void
    {
        $customer = Customer::factory()->create();

        $this->assertDatabaseHas('customers', ['id' => $customer->id]);
    }

    public function test_phone_must_be_unique(): void
    {
        Customer::factory()->create(['phone' => '+60123456789']);

        $this->expectException(QueryException::class);

        Customer::factory()->create(['phone' => '+60123456789']);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Feature/CustomerTest.php`
Expected: FAIL — `Class "App\Models\Customer" not found`.

- [ ] **Step 3: Create the migration**

Run: `php artisan make:migration create_customers_table --create=customers`

Open the generated file and replace `up()`/`down()`:

```php
    public function up(): void
    {
        Schema::create('customers', function (Blueprint $table) {
            $table->id();
            $table->string('name');
            $table->string('phone')->unique();
            $table->string('email');
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('customers');
    }
```

- [ ] **Step 4: Create the model**

Create `RideReady - PHP/app/Models/Customer.php`:

```php
<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;

class Customer extends Model
{
    use HasFactory;

    protected $fillable = ['name', 'phone', 'email'];

    public function bookings(): HasMany
    {
        return $this->hasMany(Booking::class);
    }
}
```

- [ ] **Step 5: Create the factory**

Create `RideReady - PHP/database/factories/CustomerFactory.php`:

```php
<?php

namespace Database\Factories;

use Illuminate\Database\Eloquent\Factories\Factory;

class CustomerFactory extends Factory
{
    public function definition(): array
    {
        return [
            'name' => $this->faker->name(),
            'phone' => '+60'.$this->faker->unique()->numerify('1########'),
            'email' => $this->faker->safeEmail(),
        ];
    }
}
```

- [ ] **Step 6: Run migrations and the test**

Run:
```bash
php artisan migrate
php artisan test tests/Feature/CustomerTest.php
```
Expected: `Tests:  2 passed`.

- [ ] **Step 7: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Models/Customer.php" "RideReady - PHP/database/migrations" "RideReady - PHP/database/factories/CustomerFactory.php" "RideReady - PHP/tests/Feature/CustomerTest.php"
git commit -m "feat(php): add Customer model, migration, and factory"
```

---

### Task 6: PricingSetting model, migration, seeder, and factory

**Files:**
- Create: `RideReady - PHP/app/Models/PricingSetting.php`
- Create: `RideReady - PHP/database/migrations/<timestamp>_create_pricing_settings_table.php`
- Create: `RideReady - PHP/database/factories/PricingSettingFactory.php`
- Create: `RideReady - PHP/database/seeders/PricingSettingSeeder.php`
- Test: `RideReady - PHP/tests/Feature/PricingSettingSeederTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/PricingSettingSeederTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Models\PricingSetting;
use Database\Seeders\PricingSettingSeeder;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class PricingSettingSeederTest extends TestCase
{
    use RefreshDatabase;

    public function test_seeds_one_active_row_per_vehicle_type(): void
    {
        (new PricingSettingSeeder())->run();

        $this->assertSame(3, PricingSetting::where('is_active', true)->count());
        $this->assertNotNull(PricingSetting::where('vehicle_type', 'Car')->first());
        $this->assertNotNull(PricingSetting::where('vehicle_type', 'Van')->first());
        $this->assertNotNull(PricingSetting::where('vehicle_type', 'Bus')->first());
    }

    public function test_running_it_twice_does_not_duplicate_rows(): void
    {
        (new PricingSettingSeeder())->run();
        (new PricingSettingSeeder())->run();

        $this->assertSame(3, PricingSetting::count());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Feature/PricingSettingSeederTest.php`
Expected: FAIL — `Class "App\Models\PricingSetting" not found`.

- [ ] **Step 3: Create the migration**

Run: `php artisan make:migration create_pricing_settings_table --create=pricing_settings`

Open the generated file and replace `up()`/`down()`:

```php
    public function up(): void
    {
        Schema::create('pricing_settings', function (Blueprint $table) {
            $table->id();
            $table->string('vehicle_type');
            $table->decimal('base_fare', 10, 2);
            $table->decimal('per_km_rate', 10, 2);
            $table->decimal('per_hour_rate', 10, 2);
            $table->unsignedInteger('first_km_distance');
            $table->decimal('first_km_charge', 10, 2)->nullable();
            $table->decimal('passenger_surcharge', 10, 2)->nullable();
            $table->decimal('luggage_fee_per_extra', 10, 2)->default(5);
            $table->decimal('service_tax_percent', 5, 2);
            $table->boolean('is_active')->default(true);
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('pricing_settings');
    }
```

- [ ] **Step 4: Create the model**

Create `RideReady - PHP/app/Models/PricingSetting.php`:

```php
<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class PricingSetting extends Model
{
    use HasFactory;

    protected $fillable = [
        'vehicle_type', 'base_fare', 'per_km_rate', 'per_hour_rate',
        'first_km_distance', 'first_km_charge', 'passenger_surcharge',
        'luggage_fee_per_extra', 'service_tax_percent', 'is_active',
    ];

    protected $casts = [
        'is_active' => 'boolean',
    ];
}
```

- [ ] **Step 5: Create the factory**

Create `RideReady - PHP/database/factories/PricingSettingFactory.php`:

```php
<?php

namespace Database\Factories;

use Illuminate\Database\Eloquent\Factories\Factory;

class PricingSettingFactory extends Factory
{
    public function definition(): array
    {
        return [
            'vehicle_type' => 'Car',
            'base_fare' => 20,
            'per_km_rate' => 1.5,
            'per_hour_rate' => 10,
            'first_km_distance' => 5,
            'first_km_charge' => 10,
            'passenger_surcharge' => 3,
            'luggage_fee_per_extra' => 5,
            'service_tax_percent' => 6,
            'is_active' => true,
        ];
    }
}
```

- [ ] **Step 6: Create the seeder**

Create `RideReady - PHP/database/seeders/PricingSettingSeeder.php`:

```php
<?php

namespace Database\Seeders;

use App\Models\PricingSetting;
use Illuminate\Database\Seeder;

class PricingSettingSeeder extends Seeder
{
    public function run(): void
    {
        $rows = [
            ['vehicle_type' => 'Car', 'base_fare' => 20, 'per_km_rate' => 1.5, 'per_hour_rate' => 10, 'first_km_distance' => 5, 'first_km_charge' => 10, 'passenger_surcharge' => 3, 'luggage_fee_per_extra' => 5, 'service_tax_percent' => 6],
            ['vehicle_type' => 'Van', 'base_fare' => 35, 'per_km_rate' => 2, 'per_hour_rate' => 15, 'first_km_distance' => 5, 'first_km_charge' => 15, 'passenger_surcharge' => 5, 'luggage_fee_per_extra' => 5, 'service_tax_percent' => 6],
            ['vehicle_type' => 'Bus', 'base_fare' => 60, 'per_km_rate' => 3, 'per_hour_rate' => 20, 'first_km_distance' => 5, 'first_km_charge' => 25, 'passenger_surcharge' => 8, 'luggage_fee_per_extra' => 8, 'service_tax_percent' => 6],
        ];

        foreach ($rows as $row) {
            PricingSetting::firstOrCreate(
                ['vehicle_type' => $row['vehicle_type']],
                $row + ['is_active' => true],
            );
        }
    }
}
```

- [ ] **Step 7: Wire both seeders into `DatabaseSeeder`**

Replace the contents of `RideReady - PHP/database/seeders/DatabaseSeeder.php`:

```php
<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;

class DatabaseSeeder extends Seeder
{
    public function run(): void
    {
        $this->call([
            AdminSeeder::class,
            PricingSettingSeeder::class,
        ]);
    }
}
```

- [ ] **Step 8: Run migrations and the test**

Run:
```bash
php artisan migrate
php artisan test tests/Feature/PricingSettingSeederTest.php
```
Expected: `Tests:  2 passed`.

- [ ] **Step 9: Seed the dev database and commit**

```bash
php artisan db:seed
```
Expected: no errors — creates the dev admin and 3 pricing rows in `rideready_php`.

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Models/PricingSetting.php" "RideReady - PHP/database/migrations" "RideReady - PHP/database/factories/PricingSettingFactory.php" "RideReady - PHP/database/seeders" "RideReady - PHP/tests/Feature/PricingSettingSeederTest.php"
git commit -m "feat(php): add PricingSetting model, migration, seeder, and factory"
```

---

### Task 7: Booking and BookingQuote models, migrations, and factory

**Files:**
- Create: `RideReady - PHP/app/Models/Booking.php`
- Create: `RideReady - PHP/app/Models/BookingQuote.php`
- Create: `RideReady - PHP/database/migrations/<timestamp>_create_bookings_table.php`
- Create: `RideReady - PHP/database/migrations/<timestamp>_create_booking_quotes_table.php`
- Create: `RideReady - PHP/database/factories/BookingFactory.php`
- Test: `RideReady - PHP/tests/Feature/BookingModelTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/BookingModelTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Models\Booking;
use App\Models\Customer;
use Illuminate\Database\QueryException;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class BookingModelTest extends TestCase
{
    use RefreshDatabase;

    public function test_a_booking_belongs_to_a_customer_and_can_have_a_quote(): void
    {
        $customer = Customer::factory()->create();
        $booking = Booking::factory()->for($customer)->create();
        $booking->quote()->create([
            'base_fare' => 20, 'distance_km' => 10, 'distance_charge' => 10,
            'duration_hours' => 0.5, 'time_charge' => 5, 'passenger_surcharge' => 0,
            'luggage_fee' => 0, 'subtotal' => 35, 'service_tax' => 2.1,
            'total_estimated_fare' => 37.1, 'payment_method' => 'Pay_at_Pickup',
        ]);

        $this->assertTrue($booking->customer->is($customer));
        $this->assertSame(37.1, (float) $booking->fresh()->quote->total_estimated_fare);
    }

    public function test_booking_reference_must_be_unique(): void
    {
        $customer = Customer::factory()->create();
        Booking::factory()->for($customer)->create(['booking_reference' => 'RR-DUPLICATE']);

        $this->expectException(QueryException::class);

        Booking::factory()->for($customer)->create(['booking_reference' => 'RR-DUPLICATE']);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Feature/BookingModelTest.php`
Expected: FAIL — `Class "App\Models\Booking" not found`.

- [ ] **Step 3: Create the bookings migration**

Run: `php artisan make:migration create_bookings_table --create=bookings`

Open the generated file and replace `up()`/`down()`:

```php
    public function up(): void
    {
        Schema::create('bookings', function (Blueprint $table) {
            $table->id();
            $table->string('booking_reference')->unique();
            $table->foreignId('customer_id')->constrained()->cascadeOnDelete();
            $table->string('pickup_location');
            $table->string('destination');
            $table->date('pickup_date');
            $table->time('pickup_time');
            $table->unsignedTinyInteger('passengers');
            $table->unsignedTinyInteger('bags');
            $table->string('requested_vehicle_type');
            $table->string('notes')->nullable();
            $table->string('status')->default('New');
            $table->timestamps();

            $table->index(['status', 'pickup_date']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('bookings');
    }
```

- [ ] **Step 4: Create the booking_quotes migration**

Run: `php artisan make:migration create_booking_quotes_table --create=booking_quotes`

Open the generated file and replace `up()`/`down()`:

```php
    public function up(): void
    {
        Schema::create('booking_quotes', function (Blueprint $table) {
            $table->id();
            $table->foreignId('booking_id')->unique()->constrained()->cascadeOnDelete();
            $table->decimal('base_fare', 10, 2);
            $table->decimal('distance_km', 10, 2);
            $table->decimal('distance_charge', 10, 2);
            $table->decimal('duration_hours', 10, 2);
            $table->decimal('time_charge', 10, 2);
            $table->decimal('passenger_surcharge', 10, 2);
            $table->decimal('luggage_fee', 10, 2);
            $table->decimal('subtotal', 10, 2);
            $table->decimal('service_tax', 10, 2);
            $table->decimal('total_estimated_fare', 10, 2);
            $table->decimal('actual_fare', 10, 2)->nullable();
            $table->string('payment_method');
            $table->timestamp('created_at')->useCurrent();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('booking_quotes');
    }
```

- [ ] **Step 5: Create the Booking model**

Create `RideReady - PHP/app/Models/Booking.php`:

```php
<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasOne;

class Booking extends Model
{
    use HasFactory;

    protected $fillable = [
        'booking_reference', 'customer_id', 'pickup_location', 'destination',
        'pickup_date', 'pickup_time', 'passengers', 'bags',
        'requested_vehicle_type', 'notes', 'status',
    ];

    protected $casts = [
        'pickup_date' => 'date',
    ];

    public function customer(): BelongsTo
    {
        return $this->belongsTo(Customer::class);
    }

    public function quote(): HasOne
    {
        return $this->hasOne(BookingQuote::class);
    }
}
```

- [ ] **Step 6: Create the BookingQuote model**

Create `RideReady - PHP/app/Models/BookingQuote.php`:

```php
<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class BookingQuote extends Model
{
    protected $fillable = [
        'booking_id', 'base_fare', 'distance_km', 'distance_charge',
        'duration_hours', 'time_charge', 'passenger_surcharge', 'luggage_fee',
        'subtotal', 'service_tax', 'total_estimated_fare', 'actual_fare',
        'payment_method',
    ];

    public $timestamps = false;

    public function booking(): BelongsTo
    {
        return $this->belongsTo(Booking::class);
    }
}
```

- [ ] **Step 7: Create the Booking factory**

Create `RideReady - PHP/database/factories/BookingFactory.php`:

```php
<?php

namespace Database\Factories;

use App\Models\Customer;
use Illuminate\Database\Eloquent\Factories\Factory;
use Illuminate\Support\Str;

class BookingFactory extends Factory
{
    public function definition(): array
    {
        return [
            'booking_reference' => 'RR-'.strtoupper(Str::random(8)),
            'customer_id' => Customer::factory(),
            'pickup_location' => 'Kuala Lumpur',
            'destination' => 'Petaling Jaya',
            'pickup_date' => now()->addDay()->toDateString(),
            'pickup_time' => '10:00:00',
            'passengers' => 2,
            'bags' => 1,
            'requested_vehicle_type' => 'Car',
            'status' => 'New',
        ];
    }
}
```

- [ ] **Step 8: Run migrations and the test**

Run:
```bash
php artisan migrate
php artisan test tests/Feature/BookingModelTest.php
```
Expected: `Tests:  2 passed`.

- [ ] **Step 9: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Models/Booking.php" "RideReady - PHP/app/Models/BookingQuote.php" "RideReady - PHP/database/migrations" "RideReady - PHP/database/factories/BookingFactory.php" "RideReady - PHP/tests/Feature/BookingModelTest.php"
git commit -m "feat(php): add Booking and BookingQuote models, migrations, and factory"
```

---

### Task 8: MockDistanceService

**Files:**
- Create: `RideReady - PHP/app/Services/MockDistanceService.php`
- Test: `RideReady - PHP/tests/Unit/MockDistanceServiceTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Unit/MockDistanceServiceTest.php`:

```php
<?php

namespace Tests\Unit;

use App\Services\MockDistanceService;
use PHPUnit\Framework\TestCase;

class MockDistanceServiceTest extends TestCase
{
    public function test_estimates_are_deterministic_for_the_same_route(): void
    {
        $service = new MockDistanceService();

        $distanceKm = $service->estimateDistanceKm('Kuala Lumpur', 'Petaling Jaya');
        $durationHours = $service->estimateDurationHours('Kuala Lumpur', 'Petaling Jaya');

        $this->assertSame(21.2, $distanceKm);
        $this->assertSame(0.53, $durationHours);
    }

    public function test_estimates_stay_within_a_plausible_range(): void
    {
        $service = new MockDistanceService();

        $distanceKm = $service->estimateDistanceKm('Ipoh', 'Penang');

        $this->assertGreaterThanOrEqual(3.0, $distanceKm);
        $this->assertLessThanOrEqual(60.0, $distanceKm);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Unit/MockDistanceServiceTest.php`
Expected: FAIL — `Class "App\Services\MockDistanceService" not found`.

- [ ] **Step 3: Implement the service**

Create `RideReady - PHP/app/Services/MockDistanceService.php`:

```php
<?php

namespace App\Services;

class MockDistanceService
{
    public function estimateDistanceKm(string $pickup, string $destination): float
    {
        $seed = crc32($pickup.'|'.$destination);

        return round(3 + ($seed % 5700) / 100, 1);
    }

    public function estimateDurationHours(string $pickup, string $destination): float
    {
        $distanceKm = $this->estimateDistanceKm($pickup, $destination);

        return round($distanceKm / 40, 2);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `php artisan test tests/Unit/MockDistanceServiceTest.php`
Expected: `Tests:  2 passed`.

- [ ] **Step 5: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Services/MockDistanceService.php" "RideReady - PHP/tests/Unit/MockDistanceServiceTest.php"
git commit -m "feat(php): add MockDistanceService as a stand-in for real mapping"
```

---

### Task 9: BookingService fare calculation

**Files:**
- Create: `RideReady - PHP/app/Services/BookingService.php`
- Test: `RideReady - PHP/tests/Unit/BookingFareCalculationTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Unit/BookingFareCalculationTest.php`:

```php
<?php

namespace Tests\Unit;

use App\Models\PricingSetting;
use App\Services\BookingService;
use App\Services\MockDistanceService;
use PHPUnit\Framework\TestCase;

class BookingFareCalculationTest extends TestCase
{
    private function carPricing(): PricingSetting
    {
        return new PricingSetting([
            'vehicle_type' => 'Car',
            'base_fare' => 20,
            'per_km_rate' => 1.5,
            'per_hour_rate' => 10,
            'first_km_distance' => 5,
            'first_km_charge' => 10,
            'passenger_surcharge' => 3,
            'luggage_fee_per_extra' => 5,
            'service_tax_percent' => 6,
        ]);
    }

    public function test_calculates_fare_with_distance_beyond_the_first_km_bracket(): void
    {
        $service = new BookingService(new MockDistanceService());

        $quote = $service->calculateQuote($this->carPricing(), 20, 1.5, 3, 4);

        $this->assertSame(32.5, $quote['distance_charge']);
        $this->assertSame(15.0, $quote['time_charge']);
        $this->assertSame(6.0, $quote['passenger_surcharge']);
        $this->assertSame(10.0, $quote['luggage_fee']);
        $this->assertSame(83.5, $quote['subtotal']);
        $this->assertSame(5.01, $quote['service_tax']);
        $this->assertSame(88.51, $quote['total_estimated_fare']);
    }

    public function test_calculates_fare_within_the_first_km_bracket_with_no_surcharges(): void
    {
        $service = new BookingService(new MockDistanceService());

        $quote = $service->calculateQuote($this->carPricing(), 3, 0.2, 1, 2);

        $this->assertSame(10.0, $quote['distance_charge']);
        $this->assertSame(2.0, $quote['time_charge']);
        $this->assertSame(0.0, $quote['passenger_surcharge']);
        $this->assertSame(0.0, $quote['luggage_fee']);
        $this->assertSame(32.0, $quote['subtotal']);
        $this->assertSame(1.92, $quote['service_tax']);
        $this->assertSame(33.92, $quote['total_estimated_fare']);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Unit/BookingFareCalculationTest.php`
Expected: FAIL — `Class "App\Services\BookingService" not found`.

- [ ] **Step 3: Implement `BookingService::calculateQuote`**

Create `RideReady - PHP/app/Services/BookingService.php`:

```php
<?php

namespace App\Services;

use App\Models\PricingSetting;

class BookingService
{
    public function __construct(private MockDistanceService $distanceService)
    {
    }

    public function calculateQuote(PricingSetting $pricing, float $distanceKm, float $durationHours, int $passengers, int $bags): array
    {
        $firstKmCharge = (float) ($pricing->first_km_charge ?? 0);

        $distanceCharge = $distanceKm <= $pricing->first_km_distance
            ? $firstKmCharge
            : $firstKmCharge + ($distanceKm - $pricing->first_km_distance) * (float) $pricing->per_km_rate;

        $timeCharge = $durationHours * (float) $pricing->per_hour_rate;
        $passengerSurcharge = max(0, $passengers - 1) * (float) ($pricing->passenger_surcharge ?? 0);
        $luggageFee = max(0, $bags - 2) * (float) $pricing->luggage_fee_per_extra;

        $subtotal = (float) $pricing->base_fare + $distanceCharge + $timeCharge + $passengerSurcharge + $luggageFee;
        $serviceTax = round($subtotal * ((float) $pricing->service_tax_percent / 100), 2);
        $total = round($subtotal + $serviceTax, 2);

        return [
            'base_fare' => (float) $pricing->base_fare,
            'distance_km' => $distanceKm,
            'distance_charge' => round($distanceCharge, 2),
            'duration_hours' => $durationHours,
            'time_charge' => round($timeCharge, 2),
            'passenger_surcharge' => round($passengerSurcharge, 2),
            'luggage_fee' => round($luggageFee, 2),
            'subtotal' => round($subtotal, 2),
            'service_tax' => $serviceTax,
            'total_estimated_fare' => $total,
        ];
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `php artisan test tests/Unit/BookingFareCalculationTest.php`
Expected: `Tests:  2 passed`.

- [ ] **Step 5: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Services/BookingService.php" "RideReady - PHP/tests/Unit/BookingFareCalculationTest.php"
git commit -m "feat(php): add BookingService fare calculation"
```

---

### Task 10: BookingService booking creation

**Files:**
- Create: `RideReady - PHP/app/Exceptions/BookingRejectedException.php`
- Modify: `RideReady - PHP/app/Services/BookingService.php`
- Test: `RideReady - PHP/tests/Feature/BookingServiceTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/BookingServiceTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Exceptions\BookingRejectedException;
use App\Models\Customer;
use App\Models\PricingSetting;
use App\Services\BookingService;
use App\Services\MockDistanceService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class BookingServiceTest extends TestCase
{
    use RefreshDatabase;

    private function validData(array $overrides = []): array
    {
        return array_merge([
            'customer_name' => 'Ah Meng',
            'customer_phone' => '+60123456789',
            'customer_email' => 'ahmeng@example.com',
            'pickup_location' => 'Kuala Lumpur',
            'destination' => 'Petaling Jaya',
            'pickup_date' => now()->addDay()->toDateString(),
            'pickup_time' => '10:00',
            'passengers' => 2,
            'bags' => 1,
            'vehicle_type' => 'Car',
            'notes' => null,
            'payment_method' => 'Pay_at_Pickup',
        ], $overrides);
    }

    public function test_creates_a_booking_with_a_calculated_quote(): void
    {
        PricingSetting::factory()->create(['vehicle_type' => 'Car', 'is_active' => true]);

        $booking = (new BookingService(new MockDistanceService()))->createFromRequest($this->validData());

        $this->assertNotEmpty($booking->booking_reference);
        $this->assertGreaterThan(0, $booking->quote->total_estimated_fare);
        $this->assertSame(1, Customer::where('phone', '+60123456789')->count());
    }

    public function test_reuses_an_existing_customer_by_phone(): void
    {
        PricingSetting::factory()->create(['vehicle_type' => 'Car', 'is_active' => true]);
        Customer::factory()->create(['phone' => '+60123456789']);

        (new BookingService(new MockDistanceService()))->createFromRequest($this->validData());

        $this->assertSame(1, Customer::where('phone', '+60123456789')->count());
    }

    public function test_rejects_a_pickup_in_the_past(): void
    {
        $this->expectException(BookingRejectedException::class);

        (new BookingService(new MockDistanceService()))->createFromRequest($this->validData([
            'pickup_date' => now()->subDay()->toDateString(),
        ]));
    }

    public function test_rejects_a_pickup_before_6am(): void
    {
        $this->expectException(BookingRejectedException::class);

        (new BookingService(new MockDistanceService()))->createFromRequest($this->validData([
            'pickup_time' => '05:30',
        ]));
    }

    public function test_falls_back_to_a_zeroed_quote_when_no_active_pricing_exists(): void
    {
        $booking = (new BookingService(new MockDistanceService()))->createFromRequest($this->validData());

        $this->assertSame(0.0, (float) $booking->quote->total_estimated_fare);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Feature/BookingServiceTest.php`
Expected: FAIL — `Call to undefined method App\Services\BookingService::createFromRequest()`.

- [ ] **Step 3: Create the exception**

Create `RideReady - PHP/app/Exceptions/BookingRejectedException.php`:

```php
<?php

namespace App\Exceptions;

use RuntimeException;

class BookingRejectedException extends RuntimeException
{
}
```

- [ ] **Step 4: Implement `createFromRequest`**

Modify `RideReady - PHP/app/Services/BookingService.php` — add these imports at the top:

```php
use App\Exceptions\BookingRejectedException;
use App\Models\Booking;
use App\Models\Customer;
use Carbon\Carbon;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;
use Illuminate\Support\Str;
```

Then add these methods to the `BookingService` class (alongside `calculateQuote`):

```php
    public function createFromRequest(array $data): Booking
    {
        $pickupAt = Carbon::parse($data['pickup_date'].' '.$data['pickup_time']);

        if ($pickupAt->isPast()) {
            throw new BookingRejectedException('Pickup date and time must be in the future');
        }

        if ((int) $pickupAt->format('G') < 6) {
            throw new BookingRejectedException('Bookings only available between 6AM and 12AM (midnight)');
        }

        return DB::transaction(function () use ($data, $pickupAt) {
            $customer = Customer::firstOrCreate(
                ['phone' => $data['customer_phone']],
                ['name' => $data['customer_name'], 'email' => $data['customer_email']],
            );

            $booking = Booking::create([
                'booking_reference' => $this->generateBookingReference(),
                'customer_id' => $customer->id,
                'pickup_location' => $data['pickup_location'],
                'destination' => $data['destination'],
                'pickup_date' => $pickupAt->toDateString(),
                'pickup_time' => $pickupAt->format('H:i:s'),
                'passengers' => $data['passengers'],
                'bags' => $data['bags'],
                'requested_vehicle_type' => $data['vehicle_type'],
                'notes' => $data['notes'] ?? null,
                'status' => 'New',
            ]);

            $pricing = PricingSetting::where('vehicle_type', $data['vehicle_type'])
                ->where('is_active', true)
                ->first();

            if ($pricing === null) {
                Log::warning("No active pricing for vehicle type {$data['vehicle_type']}; creating booking {$booking->booking_reference} with a zeroed quote.");
                $quote = $this->zeroedQuote();
            } else {
                $distanceKm = $this->distanceService->estimateDistanceKm($data['pickup_location'], $data['destination']);
                $durationHours = $this->distanceService->estimateDurationHours($data['pickup_location'], $data['destination']);
                $quote = $this->calculateQuote($pricing, $distanceKm, $durationHours, (int) $data['passengers'], (int) $data['bags']);
            }

            $booking->quote()->create($quote + ['payment_method' => $data['payment_method']]);

            return $booking->load('quote');
        });
    }

    private function zeroedQuote(): array
    {
        return [
            'base_fare' => 0, 'distance_km' => 0, 'distance_charge' => 0,
            'duration_hours' => 0, 'time_charge' => 0, 'passenger_surcharge' => 0,
            'luggage_fee' => 0, 'subtotal' => 0, 'service_tax' => 0, 'total_estimated_fare' => 0,
        ];
    }

    private function generateBookingReference(): string
    {
        return 'RR-'.strtoupper(Str::random(8));
    }
```

Note: `calculateQuote` uses `PricingSetting` — the import already needed for the parameter type hint should already be present from Task 9 (`use App\Models\PricingSetting;`); leave it as-is.

- [ ] **Step 5: Run the test to verify it passes**

Run: `php artisan test tests/Feature/BookingServiceTest.php`
Expected: `Tests:  5 passed`.

- [ ] **Step 6: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Exceptions/BookingRejectedException.php" "RideReady - PHP/app/Services/BookingService.php" "RideReady - PHP/tests/Feature/BookingServiceTest.php"
git commit -m "feat(php): add BookingService::createFromRequest with quote and zeroed-quote fallback"
```

---

### Task 11: Booking form request, controller, routes, and views

**Files:**
- Create: `RideReady - PHP/app/Http/Requests/StoreBookingRequest.php`
- Create: `RideReady - PHP/app/Http/Controllers/BookingController.php`
- Create: `RideReady - PHP/resources/views/layouts/app.blade.php`
- Create: `RideReady - PHP/resources/views/booking/create.blade.php`
- Create: `RideReady - PHP/resources/views/booking/confirmation.blade.php`
- Modify: `RideReady - PHP/routes/web.php`
- Test: `RideReady - PHP/tests/Feature/BookingControllerTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/BookingControllerTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Models\PricingSetting;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class BookingControllerTest extends TestCase
{
    use RefreshDatabase;

    private function validPayload(array $overrides = []): array
    {
        return array_merge([
            'customer_name' => 'Ah Meng',
            'customer_phone' => '+60123456789',
            'customer_email' => 'ahmeng@example.com',
            'pickup_location' => 'Kuala Lumpur',
            'destination' => 'Petaling Jaya',
            'pickup_date' => now()->addDay()->toDateString(),
            'pickup_time' => '10:00',
            'passengers' => 2,
            'bags' => 1,
            'vehicle_type' => 'Car',
            'notes' => '',
            'payment_method' => 'Pay_at_Pickup',
            'accepted_terms' => '1',
        ], $overrides);
    }

    public function test_shows_the_booking_form(): void
    {
        $this->get(route('booking.create'))->assertOk();
    }

    public function test_submitting_a_valid_booking_redirects_to_confirmation(): void
    {
        PricingSetting::factory()->create(['vehicle_type' => 'Car', 'is_active' => true]);

        $response = $this->post(route('booking.store'), $this->validPayload());

        $response->assertRedirect(route('booking.confirmation'));
        $this->assertDatabaseCount('bookings', 1);
    }

    public function test_rejects_a_submission_missing_required_fields(): void
    {
        $response = $this->post(route('booking.store'), $this->validPayload(['customer_name' => '']));

        $response->assertSessionHasErrors('customer_name');
        $this->assertDatabaseCount('bookings', 0);
    }

    public function test_shows_a_top_level_error_for_a_past_pickup(): void
    {
        $response = $this->post(route('booking.store'), $this->validPayload([
            'pickup_date' => now()->subDay()->toDateString(),
        ]));

        $response->assertSessionHasErrors('booking');
        $this->assertDatabaseCount('bookings', 0);
    }

    public function test_shows_the_confirmation_page_after_booking(): void
    {
        PricingSetting::factory()->create(['vehicle_type' => 'Car', 'is_active' => true]);

        $this->post(route('booking.store'), $this->validPayload());
        $response = $this->get(route('booking.confirmation'));

        $response->assertOk()->assertSeeText('booking reference');
    }

    public function test_redirects_away_from_confirmation_without_a_prior_booking(): void
    {
        $this->get(route('booking.confirmation'))->assertRedirect(route('booking.create'));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Feature/BookingControllerTest.php`
Expected: FAIL — `Symfony\Component\Routing\Exception\RouteNotFoundException: Route [booking.create] not defined.`

- [ ] **Step 3: Create the form request**

Create `RideReady - PHP/app/Http/Requests/StoreBookingRequest.php`:

```php
<?php

namespace App\Http\Requests;

use Illuminate\Foundation\Http\FormRequest;

class StoreBookingRequest extends FormRequest
{
    public function authorize(): bool
    {
        return true;
    }

    public function rules(): array
    {
        return [
            'customer_name' => ['required', 'string', 'min:3', 'max:100'],
            'customer_phone' => ['required', 'regex:/^(\+60[0-9]{9,10}|0[0-9]{1,2}-?[0-9]{7,8})$/'],
            'customer_email' => ['required', 'email'],
            'pickup_location' => ['required', 'string', 'min:5', 'max:255'],
            'destination' => ['required', 'string', 'min:5', 'max:255'],
            'pickup_date' => ['required', 'date'],
            'pickup_time' => ['required', 'date_format:H:i'],
            'passengers' => ['required', 'integer', 'min:1', 'max:8'],
            'bags' => ['required', 'integer', 'min:0', 'max:10'],
            'vehicle_type' => ['required', 'in:Car,Van,Bus'],
            'notes' => ['nullable', 'string', 'max:500'],
            'payment_method' => ['required', 'in:Pay_at_Pickup,Bank_Transfer'],
            'accepted_terms' => ['accepted'],
        ];
    }
}
```

- [ ] **Step 4: Create the controller**

Create `RideReady - PHP/app/Http/Controllers/BookingController.php`:

```php
<?php

namespace App\Http\Controllers;

use App\Exceptions\BookingRejectedException;
use App\Http\Requests\StoreBookingRequest;
use App\Services\BookingService;
use Illuminate\Http\RedirectResponse;
use Illuminate\View\View;

class BookingController extends Controller
{
    public function create(): View
    {
        return view('booking.create');
    }

    public function store(StoreBookingRequest $request, BookingService $bookingService): RedirectResponse
    {
        try {
            $booking = $bookingService->createFromRequest($request->validated());
        } catch (BookingRejectedException $e) {
            return back()->withInput()->withErrors(['booking' => $e->getMessage()]);
        }

        session()->flash('booking_reference', $booking->booking_reference);

        return redirect()->route('booking.confirmation');
    }

    public function confirmation(): View|RedirectResponse
    {
        $reference = session('booking_reference');

        if (! $reference) {
            return redirect()->route('booking.create');
        }

        return view('booking.confirmation', ['reference' => $reference]);
    }
}
```

- [ ] **Step 5: Create the shared layout**

Create `RideReady - PHP/resources/views/layouts/app.blade.php`:

```blade
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>@yield('title', 'RideReady')</title>
    <style>
        body { font-family: sans-serif; max-width: 640px; margin: 2rem auto; padding: 0 1rem; }
        label { display: block; margin-top: 1rem; font-weight: bold; }
        input, select, textarea { width: 100%; padding: 0.5rem; margin-top: 0.25rem; box-sizing: border-box; }
        .error { color: #b00020; font-size: 0.9rem; }
        button { margin-top: 1.5rem; padding: 0.6rem 1.2rem; }
        table { width: 100%; border-collapse: collapse; margin-top: 1rem; }
        th, td { border: 1px solid #ccc; padding: 0.5rem; text-align: left; font-size: 0.9rem; }
    </style>
</head>
<body>
    @yield('content')
</body>
</html>
```

- [ ] **Step 6: Create the booking form view**

Create `RideReady - PHP/resources/views/booking/create.blade.php`:

```blade
@extends('layouts.app')

@section('title', 'Book a ride')

@section('content')
    <h1>Book a ride</h1>

    @if ($errors->has('booking'))
        <p class="error">{{ $errors->first('booking') }}</p>
    @endif

    <form method="POST" action="{{ route('booking.store') }}">
        @csrf

        <label for="customer_name">Full name</label>
        <input type="text" id="customer_name" name="customer_name" value="{{ old('customer_name') }}">
        @error('customer_name') <p class="error">{{ $message }}</p> @enderror

        <label for="customer_phone">Phone</label>
        <input type="text" id="customer_phone" name="customer_phone" value="{{ old('customer_phone') }}" placeholder="+60123456789 or 012-3456789">
        @error('customer_phone') <p class="error">{{ $message }}</p> @enderror

        <label for="customer_email">Email</label>
        <input type="email" id="customer_email" name="customer_email" value="{{ old('customer_email') }}">
        @error('customer_email') <p class="error">{{ $message }}</p> @enderror

        <label for="pickup_location">Pickup location</label>
        <input type="text" id="pickup_location" name="pickup_location" value="{{ old('pickup_location') }}">
        @error('pickup_location') <p class="error">{{ $message }}</p> @enderror

        <label for="destination">Destination</label>
        <input type="text" id="destination" name="destination" value="{{ old('destination') }}">
        @error('destination') <p class="error">{{ $message }}</p> @enderror

        <label for="pickup_date">Pickup date</label>
        <input type="date" id="pickup_date" name="pickup_date" value="{{ old('pickup_date') }}">
        @error('pickup_date') <p class="error">{{ $message }}</p> @enderror

        <label for="pickup_time">Pickup time</label>
        <input type="time" id="pickup_time" name="pickup_time" value="{{ old('pickup_time') }}">
        @error('pickup_time') <p class="error">{{ $message }}</p> @enderror

        <label for="passengers">Passengers</label>
        <input type="number" id="passengers" name="passengers" min="1" max="8" value="{{ old('passengers', 1) }}">
        @error('passengers') <p class="error">{{ $message }}</p> @enderror

        <label for="bags">Bags</label>
        <input type="number" id="bags" name="bags" min="0" max="10" value="{{ old('bags', 0) }}">
        @error('bags') <p class="error">{{ $message }}</p> @enderror

        <label for="vehicle_type">Vehicle type</label>
        <select id="vehicle_type" name="vehicle_type">
            <option value="">Select...</option>
            <option value="Car" @selected(old('vehicle_type') === 'Car')>Car</option>
            <option value="Van" @selected(old('vehicle_type') === 'Van')>Van</option>
            <option value="Bus" @selected(old('vehicle_type') === 'Bus')>Bus</option>
        </select>
        @error('vehicle_type') <p class="error">{{ $message }}</p> @enderror

        <label for="notes">Notes</label>
        <textarea id="notes" name="notes">{{ old('notes') }}</textarea>
        @error('notes') <p class="error">{{ $message }}</p> @enderror

        <label for="payment_method">Payment method</label>
        <select id="payment_method" name="payment_method">
            <option value="Pay_at_Pickup" @selected(old('payment_method', 'Pay_at_Pickup') === 'Pay_at_Pickup')>Pay at pickup</option>
            <option value="Bank_Transfer" @selected(old('payment_method') === 'Bank_Transfer')>Bank transfer</option>
        </select>
        @error('payment_method') <p class="error">{{ $message }}</p> @enderror

        <label>
            <input type="checkbox" name="accepted_terms" value="1" style="width:auto;display:inline-block;">
            I accept the terms and conditions
        </label>
        @error('accepted_terms') <p class="error">{{ $message }}</p> @enderror

        <button type="submit">Book now</button>
    </form>
@endsection
```

- [ ] **Step 7: Create the confirmation view**

Create `RideReady - PHP/resources/views/booking/confirmation.blade.php`:

```blade
@extends('layouts.app')

@section('title', 'Booking confirmed')

@section('content')
    <h1>Booking confirmed</h1>
    <p>Your booking reference is <strong>{{ $reference }}</strong>. We'll contact you to confirm your driver.</p>
@endsection
```

- [ ] **Step 8: Add the routes**

Replace the contents of `RideReady - PHP/routes/web.php`:

```php
<?php

use App\Http\Controllers\BookingController;
use Illuminate\Support\Facades\Route;

Route::redirect('/', '/booking');

Route::get('/booking', [BookingController::class, 'create'])->name('booking.create');
Route::post('/booking', [BookingController::class, 'store'])->name('booking.store');
Route::get('/booking/confirmation', [BookingController::class, 'confirmation'])->name('booking.confirmation');
```

- [ ] **Step 9: Run the test to verify it passes**

Run: `php artisan test tests/Feature/BookingControllerTest.php`
Expected: `Tests:  6 passed`.

- [ ] **Step 10: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Http/Requests/StoreBookingRequest.php" "RideReady - PHP/app/Http/Controllers/BookingController.php" "RideReady - PHP/resources/views/layouts" "RideReady - PHP/resources/views/booking" "RideReady - PHP/routes/web.php" "RideReady - PHP/tests/Feature/BookingControllerTest.php"
git commit -m "feat(php): add guest booking form, controller, and confirmation page"
```

---

### Task 12: WhatsAppService

**Files:**
- Create: `RideReady - PHP/app/Services/WhatsAppService.php`
- Modify: `RideReady - PHP/config/services.php`
- Test: `RideReady - PHP/tests/Feature/WhatsAppServiceTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/WhatsAppServiceTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Models\Booking;
use App\Models\Customer;
use App\Services\WhatsAppService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Log;
use Tests\TestCase;

class WhatsAppServiceTest extends TestCase
{
    use RefreshDatabase;

    private function fakeWhatsAppConfig(): void
    {
        config([
            'services.whatsapp.api_url' => 'https://graph.facebook.com/v20.0',
            'services.whatsapp.phone_number_id' => '1234567890',
            'services.whatsapp.access_token' => 'test-token',
            'services.whatsapp.operator_phone' => '0123456789',
        ]);
    }

    public function test_sends_a_normalized_operator_notice(): void
    {
        $this->fakeWhatsAppConfig();
        Http::fake(['*' => Http::response(['messages' => [['id' => 'wamid.abc']]], 200)]);

        $customer = Customer::factory()->create();
        $booking = Booking::factory()->for($customer)->create([
            'booking_reference' => 'RR-ABC12345',
            'pickup_location' => 'Kuala Lumpur',
            'destination' => 'Petaling Jaya',
            'pickup_date' => '2026-10-01',
            'pickup_time' => '09:30:00',
        ]);

        (new WhatsAppService())->sendOperatorBookingNotice($booking);

        Http::assertSent(function ($request) {
            return $request->url() === 'https://graph.facebook.com/v20.0/1234567890/messages'
                && $request['to'] === '60123456789'
                && $request['text']['body'] === 'New booking RR-ABC12345: Kuala Lumpur -> Petaling Jaya on 2026-10-01 09:30.'
                && $request->hasHeader('Authorization', 'Bearer test-token');
        });
    }

    public function test_a_failed_send_is_logged_and_does_not_throw(): void
    {
        $this->fakeWhatsAppConfig();
        Http::fake(['*' => Http::response('invalid token', 401)]);
        Log::shouldReceive('warning')->once();

        $customer = Customer::factory()->create();
        $booking = Booking::factory()->for($customer)->create(['booking_reference' => 'RR-FAIL0001']);

        (new WhatsAppService())->sendOperatorBookingNotice($booking);

        $this->assertTrue(true);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Feature/WhatsAppServiceTest.php`
Expected: FAIL — `Class "App\Services\WhatsAppService" not found`.

- [ ] **Step 3: Add the WhatsApp config block**

In `RideReady - PHP/config/services.php`, add this entry to the array returned by the file (alongside the existing `postmark`/`resend`/`slack` entries):

```php
    'whatsapp' => [
        'api_url' => env('WHATSAPP_API_URL'),
        'access_token' => env('WHATSAPP_ACCESS_TOKEN'),
        'phone_number_id' => env('WHATSAPP_PHONE_NUMBER_ID'),
        'operator_phone' => env('WHATSAPP_OPERATOR_PHONE'),
    ],
```

- [ ] **Step 4: Implement the service**

Create `RideReady - PHP/app/Services/WhatsAppService.php`:

```php
<?php

namespace App\Services;

use App\Models\Booking;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Log;
use RuntimeException;
use Throwable;

class WhatsAppService
{
    public function sendOperatorBookingNotice(Booking $booking): void
    {
        $message = sprintf(
            'New booking %s: %s -> %s on %s %s.',
            $booking->booking_reference,
            $booking->pickup_location,
            $booking->destination,
            $booking->pickup_date->format('Y-m-d'),
            substr($booking->pickup_time, 0, 5),
        );

        try {
            $this->send(config('services.whatsapp.operator_phone'), $message);
        } catch (Throwable $e) {
            Log::warning("WhatsApp notice failed for booking {$booking->booking_reference}: {$e->getMessage()}");
        }
    }

    private function send(string $toPhone, string $message): void
    {
        $url = sprintf('%s/%s/messages', config('services.whatsapp.api_url'), config('services.whatsapp.phone_number_id'));

        $response = Http::withToken(config('services.whatsapp.access_token'))->post($url, [
            'messaging_product' => 'whatsapp',
            'to' => $this->normalizePhone($toPhone),
            'type' => 'text',
            'text' => ['body' => $message],
        ]);

        if ($response->failed()) {
            throw new RuntimeException("WhatsApp API request failed ({$response->status()}): {$response->body()}");
        }
    }

    private function normalizePhone(string $phone): string
    {
        $digits = preg_replace('/\D/', '', $phone);

        return str_starts_with($digits, '0') ? '60'.substr($digits, 1) : $digits;
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `php artisan test tests/Feature/WhatsAppServiceTest.php`
Expected: `Tests:  2 passed`.

- [ ] **Step 6: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Services/WhatsAppService.php" "RideReady - PHP/config/services.php" "RideReady - PHP/tests/Feature/WhatsAppServiceTest.php"
git commit -m "feat(php): add WhatsAppService for the operator booking notice"
```

---

### Task 13: Wire the WhatsApp notice into booking creation

**Files:**
- Modify: `RideReady - PHP/app/Http/Controllers/BookingController.php`
- Modify: `RideReady - PHP/tests/Feature/BookingControllerTest.php`

- [ ] **Step 1: Add tests for the WhatsApp behavior, and fake HTTP in the existing success-path tests**

In `RideReady - PHP/tests/Feature/BookingControllerTest.php`, add this import at the top:

```php
use Illuminate\Support\Facades\Http;
```

In `test_submitting_a_valid_booking_redirects_to_confirmation`, add `Http::fake();` as the first line of the method body (before `PricingSetting::factory()->create(...)`).

In `test_shows_the_confirmation_page_after_booking`, likewise add `Http::fake();` as the first line of the method body.

Then add these two new test methods to the class:

```php
    public function test_sends_an_operator_whatsapp_notice_on_success(): void
    {
        config([
            'services.whatsapp.api_url' => 'https://graph.facebook.com/v20.0',
            'services.whatsapp.phone_number_id' => '1234567890',
            'services.whatsapp.access_token' => 'test-token',
            'services.whatsapp.operator_phone' => '0199998888',
        ]);
        Http::fake(['*' => Http::response(['messages' => [['id' => 'wamid.abc']]], 200)]);
        PricingSetting::factory()->create(['vehicle_type' => 'Car', 'is_active' => true]);

        $this->post(route('booking.store'), $this->validPayload());

        Http::assertSent(fn ($request) => $request['to'] === '60199998888');
    }

    public function test_a_failed_whatsapp_send_does_not_block_the_booking(): void
    {
        config([
            'services.whatsapp.api_url' => 'https://graph.facebook.com/v20.0',
            'services.whatsapp.phone_number_id' => '1234567890',
            'services.whatsapp.access_token' => 'test-token',
            'services.whatsapp.operator_phone' => '0199998888',
        ]);
        Http::fake(['*' => Http::response('bad request', 400)]);
        PricingSetting::factory()->create(['vehicle_type' => 'Car', 'is_active' => true]);

        $response = $this->post(route('booking.store'), $this->validPayload());

        $response->assertRedirect(route('booking.confirmation'));
        $this->assertDatabaseCount('bookings', 1);
    }
```

- [ ] **Step 2: Run the tests to verify the two new ones fail**

Run: `php artisan test tests/Feature/BookingControllerTest.php --filter=whatsapp`
Expected: FAIL — `Http::assertSent` finds no matching request was sent, because `store()` doesn't call `WhatsAppService` yet.

- [ ] **Step 3: Wire `WhatsAppService` into the controller**

In `RideReady - PHP/app/Http/Controllers/BookingController.php`, add the import:

```php
use App\Services\WhatsAppService;
```

Change the `store` method signature and body to:

```php
    public function store(StoreBookingRequest $request, BookingService $bookingService, WhatsAppService $whatsAppService): RedirectResponse
    {
        try {
            $booking = $bookingService->createFromRequest($request->validated());
        } catch (BookingRejectedException $e) {
            return back()->withInput()->withErrors(['booking' => $e->getMessage()]);
        }

        $whatsAppService->sendOperatorBookingNotice($booking);

        session()->flash('booking_reference', $booking->booking_reference);

        return redirect()->route('booking.confirmation');
    }
```

- [ ] **Step 4: Run the full booking controller test file to verify it passes**

Run: `php artisan test tests/Feature/BookingControllerTest.php`
Expected: `Tests:  8 passed`.

- [ ] **Step 5: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Http/Controllers/BookingController.php" "RideReady - PHP/tests/Feature/BookingControllerTest.php"
git commit -m "feat(php): send an operator WhatsApp notice when a booking is created"
```

---

### Task 14: Admin login and logout

**Files:**
- Create: `RideReady - PHP/app/Http/Controllers/Admin/AdminAuthController.php`
- Create: `RideReady - PHP/resources/views/admin/login.blade.php`
- Modify: `RideReady - PHP/routes/web.php`
- Modify: `RideReady - PHP/app/Providers/AppServiceProvider.php`
- Test: `RideReady - PHP/tests/Feature/AdminAuthTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/AdminAuthTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Models\Admin;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\Auth;
use Tests\TestCase;

class AdminAuthTest extends TestCase
{
    use RefreshDatabase;

    public function test_shows_the_login_form(): void
    {
        $this->get(route('admin.login'))->assertOk();
    }

    public function test_logs_in_with_correct_credentials(): void
    {
        Admin::factory()->create(['username' => 'operator', 'password' => 'secret123']);

        $response = $this->post(route('admin.login.submit'), [
            'username' => 'operator',
            'password' => 'secret123',
        ]);

        $response->assertRedirect(route('admin.dashboard'));
        $this->assertTrue(Auth::guard('admin')->check());
    }

    public function test_rejects_incorrect_credentials(): void
    {
        Admin::factory()->create(['username' => 'operator', 'password' => 'secret123']);

        $response = $this->post(route('admin.login.submit'), [
            'username' => 'operator',
            'password' => 'wrong-password',
        ]);

        $response->assertSessionHasErrors('username');
        $this->assertFalse(Auth::guard('admin')->check());
    }

    public function test_logs_out(): void
    {
        $admin = Admin::factory()->create();

        $this->actingAs($admin, 'admin')
            ->post(route('admin.logout'))
            ->assertRedirect(route('admin.login'));

        $this->assertFalse(Auth::guard('admin')->check());
    }
}
```

- [ ] **Step 2: Create the Admin factory (needed by this test)**

Create `RideReady - PHP/database/factories/AdminFactory.php`:

```php
<?php

namespace Database\Factories;

use Illuminate\Database\Eloquent\Factories\Factory;

class AdminFactory extends Factory
{
    public function definition(): array
    {
        return [
            'username' => $this->faker->unique()->userName(),
            'password' => 'password',
        ];
    }
}
```

Add `use Illuminate\Database\Eloquent\Factories\HasFactory;` and `use HasFactory;` to `RideReady - PHP/app/Models/Admin.php` (alongside the existing `Authenticatable` import and class body).

- [ ] **Step 3: Run the test to verify it fails**

Run: `php artisan test tests/Feature/AdminAuthTest.php`
Expected: FAIL — `Symfony\Component\Routing\Exception\RouteNotFoundException: Route [admin.login] not defined.`

- [ ] **Step 4: Create the controller**

Create `RideReady - PHP/app/Http/Controllers/Admin/AdminAuthController.php`:

```php
<?php

namespace App\Http\Controllers\Admin;

use App\Http\Controllers\Controller;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Illuminate\View\View;

class AdminAuthController extends Controller
{
    public function showLoginForm(): View
    {
        return view('admin.login');
    }

    public function login(Request $request): RedirectResponse
    {
        $credentials = $request->validate([
            'username' => ['required', 'string'],
            'password' => ['required', 'string'],
        ]);

        if (! Auth::guard('admin')->attempt($credentials)) {
            return back()->withInput($request->only('username'))->withErrors([
                'username' => 'Invalid username or password',
            ]);
        }

        $request->session()->regenerate();

        return redirect()->route('admin.dashboard');
    }

    public function logout(Request $request): RedirectResponse
    {
        Auth::guard('admin')->logout();
        $request->session()->invalidate();
        $request->session()->regenerateToken();

        return redirect()->route('admin.login');
    }
}
```

- [ ] **Step 5: Create the login view**

Create `RideReady - PHP/resources/views/admin/login.blade.php`:

```blade
@extends('layouts.app')

@section('title', 'Admin login')

@section('content')
    <h1>Admin login</h1>

    <form method="POST" action="{{ route('admin.login.submit') }}">
        @csrf

        <label for="username">Username</label>
        <input type="text" id="username" name="username" value="{{ old('username') }}">
        @error('username') <p class="error">{{ $message }}</p> @enderror

        <label for="password">Password</label>
        <input type="password" id="password" name="password">
        @error('password') <p class="error">{{ $message }}</p> @enderror

        <button type="submit">Log in</button>
    </form>
@endsection
```

- [ ] **Step 6: Add the routes**

In `RideReady - PHP/routes/web.php`, add the import:

```php
use App\Http\Controllers\Admin\AdminAuthController;
```

And append at the end of the file:

```php
Route::prefix('admin')->name('admin.')->group(function () {
    Route::get('/login', [AdminAuthController::class, 'showLoginForm'])->name('login');
    Route::post('/login', [AdminAuthController::class, 'login'])->name('login.submit');
    Route::post('/logout', [AdminAuthController::class, 'logout'])->name('logout');
});
```

Note: `route('admin.dashboard')` is referenced by the controller but not defined yet — that's fine, it's only resolved when a login actually succeeds, which Task 15 will make routable. The test for successful login will still fail until Task 15 adds it (see Step 7).

- [ ] **Step 7: Set the auth redirect target**

In `RideReady - PHP/app/Providers/AppServiceProvider.php`, add this import:

```php
use Illuminate\Auth\Middleware\Authenticate;
```

And change the `boot()` method body from `//` to:

```php
    public function boot(): void
    {
        Authenticate::redirectUsing(fn () => route('admin.login'));
    }
```

- [ ] **Step 8: Run the test — expect the login-success case to still fail**

Run: `php artisan test tests/Feature/AdminAuthTest.php`
Expected: 3 of 4 pass; `test_logs_in_with_correct_credentials` FAILs with `RouteNotFoundException: Route [admin.dashboard] not defined.` — that route is added in Task 15. This is expected at this point in the plan; do not treat it as a regression.

- [ ] **Step 9: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Http/Controllers/Admin/AdminAuthController.php" "RideReady - PHP/resources/views/admin/login.blade.php" "RideReady - PHP/routes/web.php" "RideReady - PHP/app/Providers/AppServiceProvider.php" "RideReady - PHP/app/Models/Admin.php" "RideReady - PHP/database/factories/AdminFactory.php" "RideReady - PHP/tests/Feature/AdminAuthTest.php"
git commit -m "feat(php): add admin login and logout"
```

---

### Task 15: Admin dashboard (read-only bookings list)

**Files:**
- Create: `RideReady - PHP/app/Http/Controllers/Admin/AdminDashboardController.php`
- Create: `RideReady - PHP/resources/views/admin/dashboard.blade.php`
- Modify: `RideReady - PHP/routes/web.php`
- Test: `RideReady - PHP/tests/Feature/AdminDashboardTest.php`

- [ ] **Step 1: Write the failing test**

Create `RideReady - PHP/tests/Feature/AdminDashboardTest.php`:

```php
<?php

namespace Tests\Feature;

use App\Models\Admin;
use App\Models\Booking;
use App\Models\Customer;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class AdminDashboardTest extends TestCase
{
    use RefreshDatabase;

    public function test_redirects_unauthenticated_admins_to_login(): void
    {
        $this->get(route('admin.dashboard'))->assertRedirect(route('admin.login'));
    }

    public function test_lists_bookings_for_an_authenticated_admin(): void
    {
        $admin = Admin::factory()->create();
        $customer = Customer::factory()->create(['name' => 'Ah Meng']);
        Booking::factory()->for($customer)->create(['booking_reference' => 'RR-TEST1234']);

        $response = $this->actingAs($admin, 'admin')->get(route('admin.dashboard'));

        $response->assertOk();
        $response->assertSee('RR-TEST1234');
        $response->assertSee('Ah Meng');
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `php artisan test tests/Feature/AdminDashboardTest.php`
Expected: FAIL — `Symfony\Component\Routing\Exception\RouteNotFoundException: Route [admin.dashboard] not defined.`

- [ ] **Step 3: Create the controller**

Create `RideReady - PHP/app/Http/Controllers/Admin/AdminDashboardController.php`:

```php
<?php

namespace App\Http\Controllers\Admin;

use App\Http\Controllers\Controller;
use App\Models\Booking;
use Illuminate\View\View;

class AdminDashboardController extends Controller
{
    public function index(): View
    {
        $bookings = Booking::with(['customer', 'quote'])->latest()->get();

        return view('admin.dashboard', ['bookings' => $bookings]);
    }
}
```

- [ ] **Step 4: Create the dashboard view**

Create `RideReady - PHP/resources/views/admin/dashboard.blade.php`:

```blade
@extends('layouts.app')

@section('title', 'Admin dashboard')

@section('content')
    <h1>Bookings</h1>

    <form method="POST" action="{{ route('admin.logout') }}" style="margin-bottom:1rem;">
        @csrf
        <button type="submit">Log out</button>
    </form>

    <table>
        <thead>
            <tr>
                <th>Reference</th>
                <th>Customer</th>
                <th>Phone</th>
                <th>Pickup</th>
                <th>Destination</th>
                <th>Date</th>
                <th>Time</th>
                <th>Vehicle</th>
                <th>Status</th>
                <th>Est. fare</th>
            </tr>
        </thead>
        <tbody>
            @forelse ($bookings as $booking)
                <tr>
                    <td>{{ $booking->booking_reference }}</td>
                    <td>{{ $booking->customer->name }}</td>
                    <td>{{ $booking->customer->phone }}</td>
                    <td>{{ $booking->pickup_location }}</td>
                    <td>{{ $booking->destination }}</td>
                    <td>{{ $booking->pickup_date->format('Y-m-d') }}</td>
                    <td>{{ substr($booking->pickup_time, 0, 5) }}</td>
                    <td>{{ $booking->requested_vehicle_type }}</td>
                    <td>{{ $booking->status }}</td>
                    <td>{{ number_format($booking->quote->total_estimated_fare ?? 0, 2) }}</td>
                </tr>
            @empty
                <tr><td colspan="10">No bookings yet.</td></tr>
            @endforelse
        </tbody>
    </table>
@endsection
```

- [ ] **Step 5: Add the protected route**

In `RideReady - PHP/routes/web.php`, add the import:

```php
use App\Http\Controllers\Admin\AdminDashboardController;
```

And inside the existing `Route::prefix('admin')->name('admin.')->group(function () { ... });` block from Task 14, add:

```php
    Route::middleware('auth:admin')->get('/', [AdminDashboardController::class, 'index'])->name('dashboard');
```

- [ ] **Step 6: Run this task's test, then the full suite**

Run:
```bash
php artisan test tests/Feature/AdminDashboardTest.php
```
Expected: `Tests:  2 passed`.

Then run the full suite to confirm Task 14's previously-expected failure is now fixed too:
```bash
php artisan test
```
Expected: all tests pass, with no failures.

- [ ] **Step 7: Commit**

```bash
cd "/c/LitXus Systems/RideReady"
git add "RideReady - PHP/app/Http/Controllers/Admin/AdminDashboardController.php" "RideReady - PHP/resources/views/admin/dashboard.blade.php" "RideReady - PHP/routes/web.php" "RideReady - PHP/tests/Feature/AdminDashboardTest.php"
git commit -m "feat(php): add read-only admin bookings dashboard"
```

---

### Task 16: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Run the full automated test suite**

Run: `php artisan test`
Expected: every test across all files passes (0 failures). If anything fails, stop and fix it before continuing — do not proceed to manual verification with a red test suite.

- [ ] **Step 2: Seed the dev database fresh**

Run:
```bash
php artisan migrate:fresh --seed
```
Expected: all migrations re-run cleanly and `db:seed` reports success (creates the dev admin from `.env`'s `ADMIN_USERNAME`/`ADMIN_PASSWORD`, and the 3 pricing rows).

- [ ] **Step 3: Start the dev server**

Run (use a tool call that supports `run_in_background: true`):
```bash
php artisan serve
```
Expected: "Server running on [http://127.0.0.1:8000]".

- [ ] **Step 4: Manually walk through the customer booking flow in a browser**

Open `http://127.0.0.1:8000/booking`. Fill in the form with a pickup date/time at least a day in the future, hour 6AM–11:59PM, and submit. Expected: redirected to a confirmation page showing a reference like `RR-XXXXXXXX`. Check `storage/logs/laravel.log` — if `WHATSAPP_ACCESS_TOKEN` is still blank in `.env` (no real WhatsApp credentials configured), you should see a logged warning like "WhatsApp notice failed for booking RR-...: WhatsApp API request failed (...)" and the booking should still have succeeded (this confirms the failure-doesn't-block behavior end-to-end, not just in tests).

- [ ] **Step 5: Manually walk through the admin flow in a browser**

Open `http://127.0.0.1:8000/admin` — expect a redirect to `/admin/login`. Log in with the username/password from `.env`'s `ADMIN_USERNAME`/`ADMIN_PASSWORD`. Expected: redirected to the dashboard, showing the booking created in Step 4 in the table. Click "Log out" and confirm it redirects back to the login page, and that visiting `/admin` again redirects to login (session cleared).

- [ ] **Step 6: Stop the dev server**

Stop the background `php artisan serve` process (and the background `mysql_start.bat` process from Task 2, if you want to free up the MySQL port afterward — otherwise it's fine to leave MariaDB running for future work).

No commit for this task — it's verification only, and Task 15 already committed everything Step 1–5 exercises.
