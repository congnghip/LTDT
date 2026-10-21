import psycopg2
conn = psycopg2.connect('dbname=buinguyencongnghiep_chamcong user=postgres password=admin host=localhost port=5432')
cur = conn.cursor()
cur.execute('DELETE FROM "Attendances" WHERE "Date" >= CURRENT_DATE;')
conn.commit()
print("Deleted rows:", cur.rowcount)
conn.close()
