using ENBT.NET;


Enbt enbt = new Dictionary<string, Enbt>();

enbt["hello"] = 123;
enbt["aaa"] = 343.21;
enbt["ssss"] = Enbt.Empty;
enbt["yyyyy"] = 263.123f;
enbt["bbbbb"] = true;
enbt["sfassa"] = Guid.NewGuid();
Enbt arr = enbt["yawn"] = new List<Enbt>(4);

arr[1] = 325;
arr[3] = 436;

using (EnbtStream writer = new("test.enbt", FileMode.Create))
{
    writer.SaveHeader();
    writer.SaveToken(enbt);
}
Console.WriteLine(enbt);



///{"hello":123, "aaa":343.21, "ssss":, "yyyyy":263.123, "bbbbb":True, "sfassa":04e7736f-8a63-4ca2-bbd9-1027d49170bb, "yawn":[, 325, , 436]}

//// test in c++ passed
//std::fstream fs;
//fs.open("test.enbt", std::ios::in);
//fs >> std::noskipws;
//ENBTHelper::CheckVersion(fs);
//ENBT to_test = ENBTHelper::ReadToken(fs);
//fs.close();
//
//
//
//
//std::cout << (int)to_test["hello"] << std::endl;
//std::cout << (double)to_test["aaa"] << std::endl;
//std::cout << (int)to_test["ssss"] << std::endl;
//std::cout << (float)to_test["yyyyy"] << std::endl;
//std::cout << (bool)to_test["bbbbb"] << std::endl;
//std::cout << (ENBT::UUID)to_test["sfassa"] << std::endl;
//std::vector<ENBT> arr = to_test["yawn"];
//for (auto & it : arr)
//    std::cout << (int)it << std::endl;
//
//// console output
//123
//343.21
//0
//263.123
//1
//04e7736f-8a63-4ca2-bbd9-1027d49170bb
//0
//325
//0
//436
