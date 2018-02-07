

MySqlDbHelper.cs类的用法

（1） 实例化该类

      MySqlDbHelper dbHelper = new MySqlDbHelper("172.17.8.130","hsdatabase","root","root","3306");
      
      参数分别为数据库的IP，数据库名称，用户名，密码，端口号
      实例化中有对数据库的连接操作，因此后面无需再进行数据库的连接操作。
      
（2） 使用该类的各种方法

      ret = dbHelper.userinfo_record_insert("dddddhdddd", "operator", "aaaaa");
      ret = dbHelper.SetApTaskStatusToReqstBySN("123456");
      byte[] bb = dbHelper.GetTaskBySN(ref tt, "123456");
      
（3） 关闭数据库

	  在程序退出的情况下关闭数据库的连接
	  rv = dbHelper.CloseDbConn();      
	  
	  
	  