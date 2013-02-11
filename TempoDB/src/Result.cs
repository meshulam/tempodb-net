using RestSharp;
using System;
using TempoDB.Utility;


namespace TempoDB
{
    public class Result<T> where T : Model
    {
        private T value;
        private bool success;
        private int code;
        private string message;

        public T Value
        {
            get { return value; }
            private set { this.value = value; }
        }

        public bool Success
        {
            get { return success; }
            private set { this.success = value; }
        }

        public int Code
        {
            get { return code; }
            private set { this.code = value; }
        }

        public string Message
        {
            get { return message; }
            private set { this.message = value; }
        }

        public Result(IRestResponse response)
        {
            int code = (int)response.StatusCode;
            Success = (code / 100) == 2;
            if(Success == true)
            {
                Value = newValueFromResponse(response);
                Message = "";
            }
            else
            {
                Message = response.Content;
            }
        }

        public Result(T value, bool success, string message="")
        {
            Value = value;
            Success = success;
            Message = message;
        }

        private T newValueFromResponse(IRestResponse response)
        {
            if(typeof(T) == typeof(Series))
            {
                return Series.FromResponse(response) as T;
            }
            else if(typeof(T) == typeof(None))
            {
                return None.FromResponse(response) as T;
            }

            throw new Exception("Unknown T: " + typeof(T).ToString());
        }

        public override bool Equals(object obj)
        {
            var other = obj as Result<T>;
            return other != null &&
                Value.Equals(other.Value) &&
                Success.Equals(other.Success) &&
                Message.Equals(other.Message);
        }

        public override int GetHashCode()
        {
            int hash = HashCodeHelper.Initialize();
            hash = HashCodeHelper.Hash(hash, Value);
            hash = HashCodeHelper.Hash(hash, Success);
            hash = HashCodeHelper.Hash(hash, Message);
            return hash;
        }
    }
}
