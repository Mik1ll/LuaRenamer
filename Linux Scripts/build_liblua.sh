#run in docker container shell

apt-get -y install build-essential &&
curl -R -O http://www.lua.org/ftp/lua-5.4.4.tar.gz &&
tar zxf lua-5.4.4.tar.gz &&
cd lua-5.4.4/src &&
echo 'MTBjMTAKPCBDRkxBR1M9IC1PMiAtV2FsbCAtV2V4dHJhIC1ETFVBX0NPTVBBVF81XzMgJChTWVNDRkxBR1MpICQoTVlDRkxBR1MpCi0tLQo+IENGTEFHUz0gLWZQSUMgLU8yIC1XYWxsIC1XZXh0cmEgLURMVUFfQ09NUEFUXzVfMyAkKFNZU0NGTEFHUykgJChNWUNGTEFHUykKMzVhMzYKPiBMVUFfU089IGxpYmx1YTU0LnNvCjQ3YzQ4CjwgQUxMX1Q9ICQoTFVBX0EpICQoTFVBX1QpICQoTFVBQ19UKQotLS0KPiBBTExfVD0gJChMVUFfQSkgJChMVUFfVCkgJChMVUFDX1QpICQoTFVBX1NPKQo0OGE1MAo+IEFMTF9TTz0gJChMVUFfU08pCjU4YTYxLDYyCj4gc286ICQoQUxMX1NPKQo+IAo2MWE2Niw2OAo+IAo+ICQoTFVBX1NPKTogJChCQVNFX08pCj4gCSQoQ0MpIC1zaGFyZWQgLW8gJEAgJChMREZMQUdTKSAkKEJBU0VfTykgJChMSUJTKQo=' | base64 -d | patch Makefile &&
make linux so &&
cp liblua54.so ../../ &&
cd ../../
