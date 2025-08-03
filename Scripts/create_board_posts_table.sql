-- Create board_posts table for production schema
CREATE TABLE IF NOT EXISTS maskinen.board_posts (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255),
    message TEXT,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    user_id INTEGER NOT NULL,
    CONSTRAINT fk_board_post_user FOREIGN KEY (user_id)
        REFERENCES maskinen.users (id)
        ON DELETE CASCADE
);

-- Create board_posts table for test schema
CREATE TABLE IF NOT EXISTS test_schema.board_posts (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255),
    message TEXT,
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    user_id INTEGER NOT NULL,
    CONSTRAINT fk_board_post_user FOREIGN KEY (user_id)
        REFERENCES test_schema.users (id)
        ON DELETE CASCADE
);
