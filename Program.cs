using System.Data;
using System.Data.Sql;
using System.Data.SQLite;
using System.IO;

namespace SuppeSesh
{
    struct SqlParameter
    {
        internal string param;
        internal object value;

        internal SqlParameter(string param, object value)
        {
            this.param = param;
            this.value = value;
        }
    }
    class Program
    {
        public static SQLiteConnection sqlite_conn;
        public static SQLiteCommand sqlite_cmd;
        static void Main(string[] args)
        {
            StartDB();
        }

        static void StartDB()
        {
            sqlite_conn = new SQLiteConnection("data source = SuppeDB.db; Version = 3; New = false; Compress = true;");
            sqlite_cmd = sqlite_conn.CreateCommand();
        }

        public static SQLiteDataReader RunSqlCommand(string sql, List<SqlParameter>? parameters = null)
        {
            sqlite_cmd.CommandText = sql;
            if (parameters != null)
            {
                return sqlite_cmd.ExecuteReader();
            }

            foreach (SqlParameter parameter in parameters)
            {
                sqlite_cmd.Parameters.AddWithValue(parameter.param, parameter.value);
            }
            
            
            return sqlite_cmd.ExecuteReader();
        }


        
    }

    class Suppe
    {
        internal string navn;
        internal List<Ingrediens> ingredienser;
        internal Bouillon bouillon;

        Suppe(string navn, List<Ingrediens> ingredienser, Bouillon bouillon)
        {
            this.navn = navn;
            this.ingredienser = ingredienser;
            this.bouillon = bouillon;
        }

        Suppe(int dbId)
        {
            var data = Program.RunSqlCommand("SELECT Supper.Navn, Ingredienser.Navn FROM Supper\nJOIN SuppeIngredienser ON SuppeIngredienser.SuppeID = Supper.SuppeID\nJOIN Ingredienser ON Ingredienser.IngrediensID = SuppeIngredienser.IngrediensID;",
                new List<SqlParameter>() { new SqlParameter("@id", dbId) });

            if (data.HasRows == false)
            {
                Console.WriteLine("Ingen suppe fundet");
                return;
            }


            navn = data.GetString(0);
            bouillon = new Bouillon(dbId);


        }
        
        
        void AddSoupToDb()
        {
            AddSoupToDb(this);
        }
        
        static void AddSoupToDb(Suppe suppe)
        {
            Program.RunSqlCommand("INSERT INTO Supper (Navn, BouillonID) VALUES(@navn, (SELECT BouillonID FROM Bouillon WHERE Bouillon.Navn = @bouillon))",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", suppe.navn),
                    new SqlParameter("@bouillon",suppe.bouillon.navn)
                });
            foreach (Ingrediens ingrediens in suppe.ingredienser)
            {
                Program.RunSqlCommand("INSERT INTO SuppeIngredienser (SuppeID, IngrediensID) VALUES((SELECT SuppeID FROM Supper WHERE Supper.Navn = @suppe), (SELECT IngrediensID FROM Ingredienser WHERE Ingredienser.Navn = @ingrediens))",
                    new List<SqlParameter>()
                    {
                        new SqlParameter("@suppe", suppe.navn),
                        new SqlParameter("@ingrediens", ingrediens.navn)
                    });
            }
        }
        
        
        
    }

    class Ingrediens
    {
        internal string navn;
        internal bool vegansk;

        Ingrediens(string navn, bool vegansk)
        {
            this.navn = navn;
            this.vegansk = vegansk;
        }

        Ingrediens(int dbId)
        {
            var data = Program.RunSqlCommand("SELECT Ingredienser.Navn, Ingredienser.Vegansk FROM Ingredienser WHERE IngrediensID = @id;",
                new List<SqlParameter>() { new SqlParameter("@id", dbId) });

            if (data.HasRows == false)
            {
                Console.WriteLine("Ingen ingrediens fundet");
                return;
            }


            navn = data.GetString(0);
            vegansk = Convert.ToBoolean(data.GetInt32(1));
        }
        
        
        void AddIngrediensToDb()
        {
            AddIngrediensToDb(this);
        }

        static void AddIngrediensToDb(Ingrediens ingrediens)
        {
            Program.RunSqlCommand("INSERT INTO Ingredienser (Navn, Vegansk)\nVALUES (@navn, @vegansk);",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", ingrediens.navn),
                    new SqlParameter("@vegansk", Convert.ToInt32(ingrediens.vegansk))
                });
        }
        
        
    }

    class Menu
    {
        string navn;
        Suppe suppe;
        Drink drink;

        Menu(string navn, Suppe suppe, Drink drink)
        {
            this.navn = navn;
            this.suppe = suppe;
            this.drink = drink;
        }

        private void AddMenuToDb()
        {
            AddMenuToDb(this);
        }
        
        internal void AddMenuToDb(Menu menu)
        {
            Program.RunSqlCommand("INSERT INTO Menuer (Navn, SuppeID, DrinkID)\nVALUES(@navn, (SELECT SuppeID FROM Supper WHERE Supper.Navn = @suppe), (SELECT DrinkID FROM Drinks WHERE Drinks.Navn = @drink));",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", menu.navn),
                    new SqlParameter("@suppe", menu.suppe.navn),
                    new SqlParameter("@drink", menu.drink.navn)
                });
            
        }
        
    }

    class Drink
    {
        internal string navn;
        internal int mængde;

        Drink(string navn, int mængde)
        {
            this.navn = navn;
            this.mængde = mængde;
        }

        Drink(int dbId)
        {
            var data = Program.RunSqlCommand("SELECT Drinks.Navn, Drinks.Mængde FROM Drinks WHERE DrinkID = @id;",
                new List<SqlParameter>() { new SqlParameter("@id", dbId) });

            if (data.HasRows == false)
            {
                Console.WriteLine("Ingen drink fundet");
                return;
            }


            navn = data.GetString(0);
            mængde = data.GetInt32(1);
        }

        private void AddDrinkToDb()
        {
            AddDrinkToDb(this);
        }
        internal static void AddDrinkToDb(Drink drink)
        {
            Program.RunSqlCommand("INSERT INTO Drinks (Navn, Mængde)\nVALUES(@navn, @mængde);",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", drink.navn),
                    new SqlParameter("@mængde", drink.mængde)
                });
        }
    }

    class Bouillon
    {
        internal string navn;

        Bouillon(string navn)
        {
            this.navn = navn;
        }

        internal Bouillon(int dbId)
        {
            var data = Program.RunSqlCommand("SELECT Bouillon.Navn FROM Bouillon WHERE BouillonID = @id;",
                new List<SqlParameter>() { new SqlParameter("@id", dbId) });

            if (data.HasRows == false)
            {
                Console.WriteLine("Ingen bouillon fundet");
                return;
            }


            navn = data.GetString(0);
        }

        private void AddBouillonToDb()
        {
            AddBouillonToDb(this);
        }
        
        internal static void AddBouillonToDb(Bouillon bouillon)
        {
            Program.RunSqlCommand("INSERT INTO Bouillon (Navn)\nVALUES(@navn);",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", bouillon.navn),
                });

        }
    }
}

