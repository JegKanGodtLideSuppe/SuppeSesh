using System.Data;
using System.Data.Sql;
using System.Data.SQLite;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq.Expressions;
using System.Net.NetworkInformation;

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
            Loop();
        }

        public static void Loop()
        {
            switch (AskQuestion("Hvad vil du gøre?", new List<string>() { "Tilføje", "Slette", "Søge", "Afslut" }))
            {
                case 1:
                    Tilføj();
                    break;
                case 2:
                    Slet();
                    break;
                case 3:
                    søge();
                    break;
                case 4:
                    return;
            }
            Loop();
        }
        

        static void StartDB()
        {
            sqlite_conn = new SQLiteConnection("data source=SuppeDB.db; Version = 3; New = false; Compress = true;");
            sqlite_cmd = sqlite_conn.CreateCommand();

        }

        private static void Slet()
        {
            switch (AskQuestion("Hvad vil du slette?",new List<string>(){"Supper"}))
            {
                case 1:
                    string search = AskQuestion("hvad skal slettes?");

                    if (int.TryParse(search, out int value))
                    {
                        Suppe.DeleteSoupFromDb(value);
                        break;
                    }
                    
                    Suppe.DeleteSoupFromDb(search);
                    break;
            }
        }
        
        private static void søge()
        {
            
            switch (AskQuestion("Hvad vil du søge efter?", new List<string>(){"Supper", "ingredienser"}))
            {
                case 1:
                    List<Suppe> supper = new List<Suppe>();
                    foreach (int suppeid in Suppe.SøgSupper("%" + AskQuestion("Søgekriterie?") + "%"))
                    {
                        supper.Add(new Suppe(suppeid));
                    }
                    
                    Console.WriteLine("navn:         |Bouillon:  |Ingredienser:\n----------------------------------------");
                    foreach (Suppe suppe in supper)
                    {
                        Console.WriteLine("{0, -14}|{1, -11}|{2,13}", suppe.navn, suppe.bouillon.navn, suppe.ingredienser[0].navn);
                        foreach (Ingrediens ingrediens in suppe.ingredienser.Skip(1))
                        {
                            Console.WriteLine("                          |{0, 13}", ingrediens.navn);
                        }
                    }

                    Console.ReadLine();
                    
                    break;
                case 2:
                    break;
            }
        }
        
        private static void Tilføj()
        {
            string navn;
            switch (AskQuestion("Hvad vil du tilføje", new List<string>() { "Suppe", "Ingrediens" }))
            {
                case 1:
                    List<Bouillon> bouillons = Bouillon.SearchBouillons("");
                    Suppe suppe = new Suppe("", new List<Ingrediens>(), new Bouillon(1))
                    {
                        navn = AskQuestion("Hvad skal suppen hedde?"),
                        bouillon = bouillons[
                            AskQuestion("hvilken bouillon skal være i suppen?", bouillons.Select(i => i.navn).ToList()) - 1]
                    };

                    while (true)
                    {
                        navn = AskQuestion("Tilføj en ingrediens til suppen?");
                        
                        suppe.ingredienser.Add(new Ingrediens(navn, true));
                        
                        using (SQLiteDataReader data = Program.RunSqlCommand(
                                   "SELECT Ingredienser.Navn FROM Ingredienser WHERE Ingredienser.Navn like (@navn);",
                                   new List<SqlParameter>() { new SqlParameter("@navn", navn) }))
                        {
                            if (!data.HasRows)
                            {
                                Console.WriteLine("Ingrediensen findes ikke i databasen.");
                                if (YNQuestion("Vil du tilføje " + navn + " til databasen?"))
                                {
                                    Ingrediens.AddIngrediensToDb(new Ingrediens(navn,
                                        YNQuestion("Er " + navn + " vegansk?")));
                                }
                            }
                            

                            if (!YNQuestion("Vil du tilføje endnu en ingrediens til suppen?"))
                            {
                                break;
                            }

                        }
                    }

                    suppe.AddSoupToDb();
                    break;
                case 2:

                    navn = AskQuestion("Tilføj en ingrediens til suppen?");

                    using (SQLiteDataReader data = Program.RunSqlCommand(
                               "SELECT Ingredienser.Navn FROM Ingredienser WHERE Ingredienser.Navn like (@navn);",
                               new List<SqlParameter>() { new SqlParameter("@navn", navn) }))
                    {
                        if (data.HasRows)
                        {
                            Console.WriteLine("Ingrediensen findes allerede i databasen.");
                            if (!YNQuestion("Er du sikke på du vil tilføje " + navn + " til databasen?"))
                            {
                                break;
                            }
                        }
                        Ingrediens.AddIngrediensToDb(new Ingrediens(navn, YNQuestion("Er " + navn + " vegansk?")));
                    } 
                    break;
            }
        }
    


        public static bool YNQuestion(string question)
        {
            Console.WriteLine(question + " Y/N");

            string input = Console.ReadKey().KeyChar.ToString();
            
            if (input.ToLower() == "n")
            {
                return false;
            }
            
            if (input.ToLower() == "y")
            {
                return true;
            }
            
            Console.WriteLine("Ikke validt svar");
            return YNQuestion(question);
        }
        
        public static int AskQuestion(string question, List<string> answers)
        {
            Console.WriteLine("\n" + question);

            for (int i = 0; i < answers.Count; i++)
            {
                Console.Write("[" + (i + 1) + "] ");
                Console.WriteLine(answers[i]);
            }

            ConsoleKeyInfo keyInfo = Console.ReadKey();
            
            if (int.TryParse(keyInfo.KeyChar.ToString(), out int num))
            {
                if (num >= 1 && num <= answers.Count)
                {
                    return num;
                }
            }
            
            Console.WriteLine("Ikke valid input");

            return AskQuestion(question, answers);
        }

        public static string AskQuestion(string question)
        {
            Console.WriteLine("\n" + question);
            
            string? input = Console.ReadLine();
            if (input == null)
            {
                Console.WriteLine("Ikke valid input");
                return AskQuestion(question);
            }
            return input;
        }

        public static SQLiteDataReader RunSqlCommand(string sql, List<SqlParameter>? parameters = null)
        {
            if (sqlite_conn.State == ConnectionState.Open)
            {
                sqlite_conn.Close();
            }
            
            sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = sql;
            
            sqlite_cmd.Parameters.Clear();  
            if (parameters == null)
            {
                sqlite_conn.Open();
                return sqlite_cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            
            foreach (SqlParameter parameter in parameters)
            {
                sqlite_cmd.Parameters.AddWithValue(parameter.param, parameter.value);
            }
            
            sqlite_conn.Open();
            return  sqlite_cmd.ExecuteReader(CommandBehavior.CloseConnection);
        }
    }

    class Suppe
    {
        internal string navn;
        internal List<Ingrediens> ingredienser = new List<Ingrediens>();
        internal Bouillon bouillon;

        internal Suppe(string navn, List<Ingrediens> ingredienser, Bouillon bouillon)
        {
            this.navn = navn;
            this.ingredienser = ingredienser;
            this.bouillon = bouillon;
        }

        internal Suppe(int dbId)
        {
            List<int> ingredienserIds = new List<int>();
            int bouillonId = 0;
            using (SQLiteDataReader data = Program.RunSqlCommand(
                       "SELECT Supper.Navn, Ingredienser.IngrediensID, Supper.BouillonID FROM Supper\nJOIN SuppeIngredienser ON SuppeIngredienser.SuppeID = Supper.SuppeID\nJOIN Ingredienser ON Ingredienser.IngrediensID = SuppeIngredienser.IngrediensID\nWHERE Supper.SuppeID = @id;",
                       new List<SqlParameter>() { new SqlParameter("@id", dbId) }))
            {
                if (data.HasRows == false)
                {
                    Console.WriteLine("Ingen suppe fundet");
                    return;
                }

                while (data.Read())
                {
                    navn = data.GetString(0);
                    ingredienserIds.Add(data.GetInt32(1));
                    bouillonId = data.GetInt32(2) ;
                }
            }

            foreach (int ingrediens in ingredienserIds)
            {
                ingredienser.Add(new Ingrediens(ingrediens));
            }
            
            bouillon = new Bouillon(bouillonId);
        }

        internal static void DeleteSoupFromDb(int id)
        {
            Program.RunSqlCommand(
                "DELETE FROM SuppeIngredienser WHERE SuppeID = 1;\nDELETE FROM Supper WHERE SuppeID = @id;", 
                new List<SqlParameter>(){new SqlParameter("@id", id)});
        }

        internal static void DeleteSoupFromDb(string SøgeKriterie)
        {
            List<int> ids = SøgSupper(SøgeKriterie);

            if (!Program.YNQuestion("er du sikke på du vil slette  " + ids.Count + " suppe(r) fra databasen?"))
            {
                return;   
            }
            
            foreach (int id in ids)
            {
                DeleteSoupFromDb(id);
            }
        }
        
        internal void AddSoupToDb()
        {
            AddSoupToDb(this);
        }
        
        internal static void AddSoupToDb(Suppe suppe)
        {
            Program.RunSqlCommand("INSERT INTO Supper (Navn, BouillonID) VALUES(@navn, (SELECT BouillonID FROM Bouillon WHERE Bouillon.Navn = @bouillon))",
                new List<SqlParameter>()
                {
                    new SqlParameter("@navn", suppe.navn),
                    new SqlParameter("@bouillon",suppe.bouillon.navn)
                });
            foreach (Ingrediens ingrediens in suppe.ingredienser)
            {
                Program.RunSqlCommand("INSERT INTO SuppeIngredienser (SuppeID, IngrediensID) VALUES((SELECT SuppeID FROM Supper WHERE Supper.Navn = @suppe COLLATE NOCASE), \n(SELECT IngrediensID FROM Ingredienser WHERE Ingredienser.Navn = @ingrediens COLLATE NOCASE))",
                    new List<SqlParameter>()
                    {
                        new SqlParameter("@suppe", suppe.navn),
                        new SqlParameter("@ingrediens", ingrediens.navn)
                    });
            }
        }

        internal static List<int> SøgSupper(string søgeKriterie)
        {
            List<int> suppeIds = new List<int>();

            using (SQLiteDataReader data = Program.RunSqlCommand("SELECT SuppeID FROM Supper WHERE Supper.Navn like (@søge) COLLATE NOCASE;", new List<SqlParameter>(){new SqlParameter("@søge", søgeKriterie)}))
            {
                if (!data.HasRows)
                {
                    return suppeIds;
                }
                
                while (data.Read())
                {
                    suppeIds.Add(data.GetInt32(0));    
                }
            }
            
            return suppeIds;
        }
        
    }

    class Ingrediens
    {
        internal string navn;
        internal bool vegansk;

        internal Ingrediens(string navn, bool vegansk)
        {
            this.navn = navn;
            this.vegansk = vegansk;
        }

        internal Ingrediens(int dbId)
        {
            using (SQLiteDataReader data = Program.RunSqlCommand("SELECT Ingredienser.Navn, Ingredienser.Vegansk FROM Ingredienser WHERE IngrediensID = @id;",
                       new List<SqlParameter>() { new SqlParameter("@id", dbId) }))
            {
                if (data.HasRows == false)
                {
                    Console.WriteLine("Ingen ingrediens fundet");
                    return;
                }

                while (data.Read())
                {
                    navn = data.GetString(0);
                    vegansk = Convert.ToBoolean(data.GetInt32(1));
                }
            }
            
        }
        
        
        void AddIngrediensToDb()
        {
            AddIngrediensToDb(this);
        }

        internal static void AddIngrediensToDb(Ingrediens ingrediens)
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
            SQLiteDataReader data = Program.RunSqlCommand("SELECT Bouillon.Navn FROM Bouillon WHERE BouillonID = @id;",
                new List<SqlParameter>() { new SqlParameter("@id", dbId) });

            if (data.HasRows == false)
            {
                Console.WriteLine("Ingen bouillon fundet");
                return;
            }

            while (data.Read())
            {
                navn = data.GetString(0);
            }
            data.Close();

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

        internal static List<Bouillon> SearchBouillons(string søgeStreng)
        {
            List<Bouillon> bouillons = new List<Bouillon>();

            using (SQLiteDataReader data = Program.RunSqlCommand("SELECT Bouillon.Navn FROM Bouillon WHERE Bouillon.Navn Like @search;", 
                       new List<SqlParameter>() {new SqlParameter("@search", "%"+søgeStreng+"%")}))
            {
                if (data.HasRows == false)
                {
                    Console.WriteLine("Ingen bouilloner der mathcer søgekriteriet");
                    return bouillons;
                }

                while (data.Read())
                {
                    bouillons.Add(new Bouillon(data.GetString(0)));
                }
            }
            
            return bouillons;
        }
    }
}

