WITH courses_to_delete AS (
    SELECT c.course_id
    FROM course c
    WHERE NOT c.is_deleted
      AND (
          c.state IS NULL
          OR c.country IS NULL
          OR c.city IS NULL
          OR c.course_name !~ '[a-zA-Z]'
          OR NOT EXISTS (
              SELECT 1 FROM teebox t
              WHERE t.course_id = c.course_id AND NOT t.is_deleted
          )
      )
),
delete_holes AS (
    DELETE FROM hole
    WHERE course_id IN (SELECT course_id FROM courses_to_delete)
    RETURNING course_id
),
delete_teeboxes AS (
    DELETE FROM teebox
    WHERE course_id IN (SELECT course_id FROM courses_to_delete)
    RETURNING course_id
),
delete_maps AS (
    DELETE FROM golf_course_api_course_map
    WHERE course_id IN (SELECT course_id FROM courses_to_delete)
    RETURNING course_id
)
DELETE FROM course
WHERE course_id IN (SELECT course_id FROM courses_to_delete);


-- Test results
SELECT c.course_id, c.course_name, c.city, c.state, c.country,
       (SELECT COUNT(*) FROM teebox t WHERE t.course_id = c.course_id AND NOT t.is_deleted) AS teebox_count
FROM course c
WHERE NOT c.is_deleted
  AND (
    c.state IS NULL
        OR c.country IS NULL
        OR c.city IS NULL
        OR c.course_name !~ '[a-zA-Z]'
        OR NOT EXISTS (
        SELECT 1 FROM teebox t
        WHERE t.course_id = c.course_id AND NOT t.is_deleted
    )
    )
ORDER BY c.course_name;