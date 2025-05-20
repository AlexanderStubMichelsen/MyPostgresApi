MyPostgresApi/
├── Controllers/
│   ├── UsersController.cs
│   └── SavedImagescontroller.cs
├── Data/
│   ├── AppDbContext.cs
│   └── test.http
├── Models/
│   ├── SavedImage.cs
│   ├── ChangePasswordRequest.cs
│   └── User.cs
├── DTOs/
│   ├── UserDto.cs
│   └── SavedImagesDto.cs
├── Tests/
│   ├── Assemblyinfo.cs
│   ├── ImagesTest.cs
│   ├──  UsersTest
│   └── CustomWebApplicationFactory.cs
├── appsettings.json
├── .env
├── .env.test
├── Program.cs
└── MyPostgresApi.csproj

Metrics with http://172.105.95.18:9090/

You can try: application_httprequests_transactions,

and then hit:
execute

Select graph for visual layout.

And you can try:
http://172.105.95.18:3000/ or http://172.105.95.18:3000/d/UDdpyzz7z/prometheus-2-0-stats?orgId=1&from=now-1h&to=now&timezone=browser&refresh=1m

for Grafana

____________________________________________________________________________________________

SQL:

CREATE TABLE IF NOT EXISTS maskinen.users (
    id SERIAL PRIMARY KEY,
    name TEXT,
    email TEXT UNIQUE NOT NULL,
    password TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS maskinen.saved_images (
	id SERIAL PRIMARY KEY,
	user_id INTEGER NOT NULL,
	image_url TEXT NOT NULL,
	title TEXT,
	photographer TEXT,
	source_link TEXT,
	saved_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
	CONSTRAINT fk_user FOREIGN KEY (user_id)
		REFERENCES maskinen.users (id)
		ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS test_schema.users (
    id SERIAL PRIMARY KEY,
    name TEXT,
    email TEXT UNIQUE NOT NULL,
    password TEXT NOT NULL
); 

CREATE TABLE IF NOT EXISTS test_schema.saved_images (
	id SERIAL PRIMARY KEY,
	user_id INTEGER NOT NULL,
	image_url TEXT NOT NULL,
	title TEXT,
	photographer TEXT,
	source_link TEXT,
	saved_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
	CONSTRAINT fk_user FOREIGN KEY (user_id)
		REFERENCES test_schema.users (id)
		ON DELETE CASCADE
);
