using ENBT.NET;


Enbt enbt = new Dictionary<string, Enbt>();

enbt["hello"] = 123;
enbt["aaa"] = 343;
enbt["ssss"] = Enbt.Empty;
enbt["yyyyy"] = 263;
Enbt arr = enbt["yawn"] = new List<Enbt>(4);

arr[1] = 325;
arr[3] = 436;

using (EnbtStream writer = new("test.enbt", FileMode.Create))
{
    writer.SaveHeader();
    writer.SaveToken(enbt);
}
Console.WriteLine(enbt);


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
//std::cout << (int)to_test["aaa"] << std::endl;
//std::cout << (int)to_test["ssss"] << std::endl;
//std::cout << (int)to_test["yyyyy"] << std::endl;
//std::vector<ENBT> arr = to_test["yawn"];
//for (auto & it : arr)
//    std::cout << (int)it << std::endl;
//
//// console output
//123
//343
//0
//263
//0
//325
//0
//436