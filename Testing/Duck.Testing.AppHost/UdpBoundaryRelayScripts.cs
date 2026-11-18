namespace Duck.Testing.AppHost;

internal static class UdpBoundaryRelayScripts
{
    // Uses Perl's core Socket module, available in the pinned Crane runtime image.
    // The host relay selects its multicast interface from the route to Docker's gateway.
    public const string HostNamespace = """
        use strict; use warnings; use Socket qw(AF_INET SOCK_DGRAM SOL_SOCKET SO_REUSEADDR IPPROTO_IP IP_ADD_MEMBERSHIP IP_MULTICAST_TTL inet_aton sockaddr_in INADDR_ANY); use IO::Select;
        my $gateway=(gethostbyname('gateway.docker.internal'))[4] or die "gateway.docker.internal did not resolve";
        socket(my $route_probe,AF_INET,SOCK_DGRAM,getprotobyname('udp')) or die "route probe socket: $!";
        connect($route_probe,sockaddr_in(9,$gateway)) or die "route probe connect: $!";
        my ($probe_port,$multicast_interface)=sockaddr_in(getsockname($route_probe));
        my $vision_group=inet_aton('224.5.23.2'); my $any=pack('a4a4',$vision_group,$multicast_interface);
        socket(my $vision,AF_INET,SOCK_DGRAM,getprotobyname('udp')) or die "vision socket: $!";
        setsockopt($vision,SOL_SOCKET,SO_REUSEADDR,pack('l',1)) or die "vision reuse: $!";
        bind($vision,sockaddr_in(10020,INADDR_ANY)) or die "vision bind: $!";
        setsockopt($vision,IPPROTO_IP,IP_ADD_MEMBERSHIP,$any) or die "vision join: $!";
        socket(my $out,AF_INET,SOCK_DGRAM,getprotobyname('udp')) or die "output socket: $!";
        setsockopt($out,IPPROTO_IP,IP_MULTICAST_TTL,pack('C',1));
        setsockopt($out,IPPROTO_IP,IP_MULTICAST_IF,$multicast_interface) or die "multicast interface: $!";
        my $host=(gethostbyname('host.docker.internal'))[4] or die "host.docker.internal did not resolve";
        my $host_to=sockaddr_in(10020,$host); my $gc_to=sockaddr_in(10021,inet_aton('224.5.23.2'));
        socket(my $tracker,AF_INET,SOCK_DGRAM,getprotobyname('udp')) or die "tracker socket: $!";
        setsockopt($tracker,SOL_SOCKET,SO_REUSEADDR,pack('l',1)) or die "tracker reuse: $!";
        bind($tracker,sockaddr_in(39113,INADDR_ANY)) or die "tracker bind: $!";
        my $tracker_to=sockaddr_in(11010,inet_aton('224.5.23.2')); my $ready=IO::Select->new($vision,$tracker);
        while (1) { for my $socket ($ready->can_read) { my $peer=recv($socket,my $data,65535,0); next unless defined $peer;
          if (fileno($socket)==fileno($vision)) { send($out,$data,0,$host_to); send($out,$data,0,$gc_to); }
          else { send($out,$data,0,$tracker_to); }
        } }
        """;

    public const string BridgeIngress = """
        use strict; use warnings; use Socket qw(AF_INET SOCK_DGRAM SOL_SOCKET SO_REUSEADDR inet_aton sockaddr_in INADDR_ANY);
        my $gateway; open(my $routes,'<','/proc/net/route') or die "route table: $!"; <$routes>;
        while (my $line=<$routes>) { my @f=split(/\s+/,$line); if (@f>2 && $f[1] eq '00000000') { $gateway=pack('V',hex($f[2])); last; } }
        close($routes); die "default gateway not found" unless $gateway;
        socket(my $in,AF_INET,SOCK_DGRAM,getprotobyname('udp')) or die "ingress socket: $!";
        setsockopt($in,SOL_SOCKET,SO_REUSEADDR,pack('l',1)) or die "ingress reuse: $!";
        bind($in,sockaddr_in(39112,INADDR_ANY)) or die "ingress bind: $!";
        socket(my $out,AF_INET,SOCK_DGRAM,getprotobyname('udp')) or die "forward socket: $!";
        my $destination=sockaddr_in(39113,$gateway);
        while (1) { my $peer=recv($in,my $data,65535,0); next unless defined $peer; send($out,$data,0,$destination); }
        """;
}
