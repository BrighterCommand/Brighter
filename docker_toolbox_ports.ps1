# Forward all the ports that Huddle uses (determined by ports mapped in docker-compose) to virtual box
# This is needed to prevent you falling apart when Huddle config asks for 'localhost' which docker toolbox does not support
# Assumes that the VM is already running (uses controlvm see below article for syntax differences modifyvm where host not running)
# See: https://github.com/boot2docker/boot2docker/blob/master/doc/WORKAROUNDS.md
# If you get a set of E_FAIL errors concerning locking, check your permissions: are you admin? 

VBoxManage controlvm "default" natpf1 "tcp-port6379,tcp,,6379,,6379";
VBoxManage controlvm "default" natpf1 "tcp-port5672,tcp,,5672,,5672";
VBoxManage controlvm "default" natpf1 "tcp-port15672,tcp,,15672,,15672";
VBoxManage controlvm "default" natpf1 "tcp-port1113,tcp,,1113,,1113";
VBoxManage controlvm "default" natpf1 "tcp-port2113,tcp,,2113,,2113";
VBoxManage controlvm "default" natpf1 "tcp-port1433,tcp,,1433,,1433";
VBoxManage controlvm "default" natpf1 "tcp-port3306,tcp,,3306,,3306";
VBoxManage controlvm "default" natpf1 "tcp-port5432,tcp,,5432,,5432";
