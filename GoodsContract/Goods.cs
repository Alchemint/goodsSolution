using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using Helper = Neo.SmartContract.Framework.Helper;
using System;
using System.Numerics;
using System.ComponentModel;


namespace GoodsContract
{
    public class Goods : SmartContract
    {

        //Default multiple signature committee account
        private static readonly byte[] committee = Helper.ToScriptHash("AZ77FiX7i9mRUPF2RyuJD2L8kS6UDnQ9Y7");

        //Static param
        private const string ADMIN_ACCOUNT = "admin_account";

        [DisplayName("goodsTransfer")]
        public static event Action<byte[], byte[], byte[], BigInteger> Transferred;

        /// <summary>
        /// addr/name/symbol/desc/totalSupply
        /// </summary>
        [DisplayName("goodsInit")]
        public static event Action<byte[], byte[], byte[], byte[], BigInteger> Inited;

        public static Object Main(string operation, params object[] args)
        {
            var magicstr = "2019-06-26";

            if (Runtime.Trigger == TriggerType.Verification)
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                var callscript = ExecutionEngine.CallingScriptHash;

                if (operation == "totalSupply")
                {
                    return TotalSupply((string)args[0]);
                }
                if (operation == "name")
                {
                    return Name((string)args[0]);
                }
                if (operation == "symbol")
                {
                    return Symbol((string)args[0]);
                }
                if (operation == "decimals")
                {
                    return Decimals((string)args[0]);
                }
                if (operation == "balanceOf")
                {
                    return BalanceOf((string)args[0], (byte[])args[1]);
                }

                if (operation == "getGoodInfo")
                {
                    return GetGoodInfo((string)args[0]);
                }

                if (operation == "transfer")
                {
                    if (args.Length != 4) return false;
                    string name = (string)args[0];
                    byte[] from = (byte[])args[1];
                    byte[] to = (byte[])args[2];
                    BigInteger amount = (BigInteger)args[3];

                    if (from.Length != 20 || to.Length != 20)
                        throw new InvalidOperationException("The parameters from and to SHOULD be 20-byte addresses.");

                    if (amount <= 0)
                        throw new InvalidOperationException("The parameter amount MUST be greater than 0.");

                    if (!Runtime.CheckWitness(from) && from.AsBigInteger() != callscript.AsBigInteger())
                        return false;

                    StorageMap goodToken = Storage.CurrentContext.CreateMap(nameof(goodToken));
                    byte[] token = goodToken.Get(name);
                    if (token.Length == 0)
                        throw new InvalidOperationException("The parameter good can not be null.");

                    return transfer(name, from, to, amount);
                }
                if (operation == "init")
                {
                    if (args.Length != 5) return false;
                    string name = (string)args[0];
                    string symbol = (string)args[1];
                    BigInteger totalSupply = (BigInteger)args[2];
                    byte[] addr = (byte[])args[3];
                    string desc = (string)args[4];

                    if (!Runtime.CheckWitness(addr))
                        return false;
                    return Init(name, symbol,totalSupply,addr, desc);
                }

                if (operation == "setAccount")
                {
                    return SetAccount((string)args[0], (byte[])args[1]);
                }
                if (operation == "getAccount")
                {
                    return GetAccount((string)args[0]);
                }
                #region contract upgrade
                if (operation == "upgrade")
                {

                    if (!checkAdmin()) return false;

                    if (args.Length != 1 && args.Length != 9)
                        return false;

                    byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                    byte[] new_script = (byte[])args[0];
                    //new script should different from old script
                    if (script == new_script)
                        return false;

                    byte[] parameter_list = new byte[] { 0x07, 0x10 };
                    byte return_type = 0x05;
                    //1|0|4
                    bool need_storage = (bool)(object)05;
                    string name = "business";
                    string version = "1";
                    string author = "alchemint";
                    string email = "0";
                    string description = "alchemint";

                    if (args.Length == 9)
                    {
                        parameter_list = (byte[])args[1];
                        return_type = (byte)args[2];
                        need_storage = (bool)args[3];
                        name = (string)args[4];
                        version = (string)args[5];
                        author = (string)args[6];
                        email = (string)args[7];
                        description = (string)args[8];
                    }
                    Contract.Migrate(new_script, parameter_list, return_type, need_storage, name, version, author, email, description);
                    return true;
                }
                #endregion

            }
            return true;
        }


        private static bool checkAdmin()
        {
            StorageMap account = Storage.CurrentContext.CreateMap(nameof(account));
            byte[] currAdmin = account.Get(ADMIN_ACCOUNT);

            if (currAdmin.Length > 0)
            {

                if (!Runtime.CheckWitness(currAdmin)) return false;
            }
            else
            {
                if (!Runtime.CheckWitness(committee)) return false;
            }
            return true;
        }

        [DisplayName("getGoodInfo")]
        public static Good GetGoodInfo(string name)
        {
            if (name.Length <= 0)
                throw new InvalidOperationException("The parameter name SHOULD be longer than 0.");
            StorageMap goodToken = Storage.CurrentContext.CreateMap(nameof(goodToken));
            byte[] token = goodToken.Get(name);
            if (token.Length == 0) return null;
            return Helper.Deserialize(token) as Good;
        }

        [DisplayName("setAccount")]
        private static bool SetAccount(string key, byte[] addr)
        {
            if (key.Length <= 0)
                throw new InvalidOperationException("The parameter key SHOULD be longer than 0.");

            if (addr.Length != 20)
                throw new InvalidOperationException("The parameters addr and to SHOULD be 20-byte addresses.");

            if (!checkAdmin()) return false;

            StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
            asset.Put(key, addr);
            return true;
        }

        [DisplayName("getAccount")]
        public static byte[] GetAccount(string key)
        {
            StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
            return asset.Get(key);
        }

        [DisplayName("init")]
        public static bool Init(string name, string symbol, BigInteger totalSupply, byte[] addr, string desc)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (name.Length <= 0)
                throw new InvalidOperationException("The parameter name SHOULD be longer than 0.");

            if (totalSupply <= 0)
                throw new InvalidOperationException("The parameter totalSupply SHOULD be longer than 0.");

            StorageMap goodToken = Storage.CurrentContext.CreateMap(nameof(goodToken));
            byte[] token = goodToken.Get(name);
            if (token.Length != 0)
                throw new InvalidOperationException("The good of name SHOULD be null.");

            if (!transfer(name, null, addr, totalSupply))
                throw new InvalidOperationException("Operation is error.");

            Good t = new Good();
            t.owner = addr;
            t.decimals = 0;
            t.name = name;
            t.symbol = symbol;
            t.totalSupply = totalSupply;
            t.desc = desc;

            goodToken.Put(name, Helper.Serialize(t));

            //创建物品资产
            Inited(addr, name.AsByteArray(), symbol.AsByteArray(), desc.AsByteArray(), totalSupply);
            return true;
        }

        [DisplayName("totalSupply")]
        public static BigInteger TotalSupply(string name)
        {
            StorageMap goodToken = Storage.CurrentContext.CreateMap(nameof(goodToken));
            byte[] token = goodToken.Get(name);
            if (token.Length == 0) return 0;
            return (Helper.Deserialize(token) as Good).totalSupply;
        }

        [DisplayName("name")]
        public static string Name(string name)
        {
            StorageMap goodToken = Storage.CurrentContext.CreateMap(nameof(goodToken));
            byte[] token = goodToken.Get(name);
            if (token.Length == 0) return "";
            return (Helper.Deserialize(token) as Good).name;
        }

        [DisplayName("symbol")]
        public static string Symbol(string name)
        {
            StorageMap goodToken = Storage.CurrentContext.CreateMap(nameof(goodToken));
            byte[] token = goodToken.Get(name);
            if (token.Length == 0) return "";
            return (Helper.Deserialize(token) as Good).symbol;
        }

        [DisplayName("decimals")]
        public static byte Decimals(string name)
        {
            StorageMap goodToken = Storage.CurrentContext.CreateMap(nameof(goodToken));
            byte[] token = goodToken.Get(name);
            if (token.Length == 0) return 0;
            return (Helper.Deserialize(token) as Good).decimals;
        }

        [DisplayName("desc")]
        public static string Desc(string name)
        {
            StorageMap goodToken = Storage.CurrentContext.CreateMap(nameof(goodToken));
            byte[] token = goodToken.Get(name);
            if (token.Length == 0) return "";
            return (Helper.Deserialize(token) as Good).desc;
        }

        [DisplayName("balanceOf")]
        public static BigInteger BalanceOf(string name, byte[] addr)
        {
            if (addr.Length != 20)
                throw new InvalidOperationException("The parameter addr SHOULD be 20-byte addresses.");

            if (name.Length <= 0)
                throw new InvalidOperationException("The parameter name SHOULD be longer than 0.");

            StorageMap goodBalance = Storage.CurrentContext.CreateMap(nameof(goodBalance));
            return goodBalance.Get(name.AsByteArray().Concat(addr)).AsBigInteger();
        }


        private static bool transfer(string name, byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;

            if (from == to) return true;

            byte[] fromKey = name.AsByteArray().Concat(from);
            byte[] toKey = name.AsByteArray().Concat(to);
            StorageMap goodBalance = Storage.CurrentContext.CreateMap(nameof(goodBalance));
            if (from.Length > 0)
            {
                BigInteger from_value = goodBalance.Get(fromKey).AsBigInteger();
                if (from_value < value) return false;
                if (from_value == value)
                    goodBalance.Delete(fromKey);
                else
                    goodBalance.Put(fromKey, from_value - value);
            }
            if (to.Length > 0)
            {
                BigInteger to_value = goodBalance.Get(toKey).AsBigInteger();
                goodBalance.Put(toKey, to_value + value);
            }

            //notify
            Transferred(name.AsByteArray(), from, to, value);
            return true;
        }

        public class Good
        {
            //the owner of good
            public byte[] owner;

            //name of good
            public string name;

            //totalSupply of good
            public BigInteger totalSupply;

            //symbol of good
            public string symbol;
            //decimals 0
            public byte decimals;
            //description of good
            public string desc;
        }

    }
}